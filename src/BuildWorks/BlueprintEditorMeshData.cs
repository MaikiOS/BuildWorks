using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    // A snapshot of the rendered mesh, including assets whose CPU copy was discarded.
    // No collider or changes to the source mesh/buffer targets are needed.
    internal static class BlueprintEditorMeshData
    {
        internal sealed class SurfaceData
        {
            internal Vector3[] Positions, Normals;
            internal Vector4[] Tangents;
            internal Vector2[] UV;
            internal int[][] Submeshes;
        }

        internal static bool TryReadSurface(Mesh mesh, out SurfaceData data, out string error)
        {
            data = null; error = null;
            try
            {
                if (!mesh || mesh.vertexCount == 0 || mesh.vertexCount > 250000 || mesh.subMeshCount > 256)
                    throw new InvalidOperationException("Fog source mesh exceeds its read budget or is empty.");
                var result = new SurfaceData { Submeshes = new int[mesh.subMeshCount][] };
                if (mesh.isReadable)
                {
                    result.Positions = mesh.vertices; result.Normals = mesh.normals;
                    result.Tangents = mesh.tangents; result.UV = mesh.uv;
                }
                else
                {
                    var streams = new Dictionary<int, byte[]>();
                    Vector4[] positions = Attribute(mesh, VertexAttribute.Position, 3, streams);
                    Vector4[] normals = Attribute(mesh, VertexAttribute.Normal, 3, streams);
                    result.Tangents = Attribute(mesh, VertexAttribute.Tangent, 4, streams);
                    Vector4[] uv = Attribute(mesh, VertexAttribute.TexCoord0, 2, streams);
                    result.Positions = new Vector3[mesh.vertexCount]; result.Normals = new Vector3[mesh.vertexCount];
                    result.UV = new Vector2[mesh.vertexCount];
                    for (int i = 0; i < mesh.vertexCount; ++i)
                    { result.Positions[i] = positions[i]; result.Normals[i] = normals[i]; result.UV[i] = uv[i]; }
                }
                if (result.Positions.Length != mesh.vertexCount || result.Normals.Length != mesh.vertexCount ||
                    result.Tangents.Length != mesh.vertexCount || result.UV.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Fog source needs original positions, normals, tangents and UV0.");
                for (int i = 0; i < mesh.vertexCount; ++i)
                {
                    Vector3 p = result.Positions[i], n = result.Normals[i]; Vector4 t = result.Tangents[i]; Vector2 uv = result.UV[i];
                    if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(n.x) || !Finite(n.y) || !Finite(n.z) ||
                        !Finite(t.x) || !Finite(t.y) || !Finite(t.z) || !Finite(t.w) || !Finite(uv.x) || !Finite(uv.y))
                        throw new InvalidOperationException("Fog source contains a non-finite surface attribute.");
                }
                byte[] indexBytes = null; int indexSize = mesh.indexFormat == IndexFormat.UInt16 ? 2 : 4;
                if (!mesh.isReadable)
                    using (GraphicsBuffer buffer = mesh.GetIndexBuffer()) indexBytes = BoundedBuffer(buffer);
                int totalIndices = 0;
                for (int submesh = 0; submesh < mesh.subMeshCount; ++submesh)
                {
                    SubMeshDescriptor descriptor = mesh.GetSubMesh(submesh);
                    totalIndices = checked(totalIndices + descriptor.indexCount);
                    if (descriptor.topology != MeshTopology.Triangles || descriptor.indexCount % 3 != 0 || totalIndices > 1500000)
                        throw new InvalidOperationException("Fog source needs bounded complete triangles for every material slot.");
                    int[] indices = mesh.isReadable ? mesh.GetTriangles(submesh) : new int[descriptor.indexCount];
                    for (int i = 0; i < indices.Length; ++i)
                    {
                        if (!mesh.isReadable)
                        {
                            int offset = checked((descriptor.indexStart + i) * indexSize);
                            indices[i] = checked((indexSize == 2 ? BitConverter.ToUInt16(indexBytes, offset)
                                : checked((int)BitConverter.ToUInt32(indexBytes, offset))) + descriptor.baseVertex);
                        }
                        if (indices[i] < 0 || indices[i] >= mesh.vertexCount)
                            throw new InvalidOperationException("Fog triangle index is outside its vertex buffer.");
                    }
                    result.Submeshes[submesh] = indices;
                }
                if (totalIndices == 0) throw new InvalidOperationException("Fog source has no surface triangles.");
                data = result; return true;
            }
            catch (Exception exception)
            { error = (mesh ? mesh.name : "Missing mesh") + ": " + exception.Message; return false; }
        }

        private static Vector4[] Attribute(Mesh mesh, VertexAttribute attribute, int dimensions, Dictionary<int, byte[]> streams)
        {
            if (!mesh.HasVertexAttribute(attribute) || mesh.GetVertexAttributeDimension(attribute) < dimensions)
                throw new InvalidOperationException("Fog source missing attribute: " + attribute);
            int stream = mesh.GetVertexAttributeStream(attribute);
            if (!streams.TryGetValue(stream, out byte[] bytes))
            {
                using (GraphicsBuffer buffer = mesh.GetVertexBuffer(stream)) bytes = BoundedBuffer(buffer);
                streams.Add(stream, bytes);
            }
            int stride = mesh.GetVertexBufferStride(stream), offset = mesh.GetVertexAttributeOffset(attribute);
            VertexAttributeFormat format = mesh.GetVertexAttributeFormat(attribute); int size = ComponentSize(format);
            var result = new Vector4[mesh.vertexCount];
            for (int i = 0; i < result.Length; ++i)
                for (int axis = 0; axis < dimensions; ++axis)
                    result[i][axis] = ReadComponent(bytes, checked(i * stride + offset + axis * size), format);
            return result;
        }

        private static byte[] BoundedBuffer(GraphicsBuffer buffer)
        {
            if (buffer == null || !buffer.IsValid() || (long)buffer.count * buffer.stride > 64 * 1024 * 1024)
                throw new InvalidOperationException("Fog source buffer is unavailable or exceeds its read budget.");
            return ReadBuffer(buffer);
        }

        internal static bool TryRead(
            Mesh mesh, out Vector3[] vertices, out int[] triangles, out string error)
        {
            vertices = Array.Empty<Vector3>();
            triangles = Array.Empty<int>();
            error = null;
            try
            {
                if (!mesh || mesh.vertexCount == 0) return false;
                if (mesh.isReadable) ReadCpu(mesh, out vertices, out triangles);
                else ReadGpu(mesh, out vertices, out triangles);
                foreach (Vector3 point in vertices)
                    if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z))
                        throw new InvalidOperationException("Mesh position is not finite.");
                foreach (int index in triangles)
                    if (index < 0 || index >= vertices.Length)
                        throw new InvalidOperationException("Mesh index is outside its vertex buffer.");
                return vertices.Length > 0 && triangles.Length >= 3;
            }
            catch (Exception exception)
            {
                vertices = Array.Empty<Vector3>();
                triangles = Array.Empty<int>();
                error = (mesh ? mesh.name : "Missing mesh") + ": " + exception.Message;
                return false;
            }
        }

        private static void ReadCpu(Mesh mesh, out Vector3[] vertices, out int[] triangles)
        {
            using (Mesh.MeshDataArray access = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                Mesh.MeshData data = access[0];
                using (var native = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                {
                    data.GetVertices(native);
                    vertices = native.ToArray();
                }
                var indices = new List<int>();
                for (int subMesh = 0; subMesh < data.subMeshCount; ++subMesh)
                {
                    SubMeshDescriptor descriptor = data.GetSubMesh(subMesh);
                    if (descriptor.topology != MeshTopology.Triangles) continue;
                    using (var native = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                    {
                        data.GetIndices(native, subMesh, applyBaseVertex: true);
                        indices.AddRange(native.ToArray());
                    }
                }
                triangles = indices.ToArray();
            }
        }

        private static void ReadGpu(Mesh mesh, out Vector3[] vertices, out int[] triangles)
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
                throw new NotSupportedException("GPU mesh readback is unavailable on this device.");
            if (!mesh.HasVertexAttribute(VertexAttribute.Position) ||
                mesh.GetVertexAttributeDimension(VertexAttribute.Position) < 3)
                throw new InvalidOperationException("Mesh has no three-component position attribute.");
            int stream = mesh.GetVertexAttributeStream(VertexAttribute.Position);
            int stride = mesh.GetVertexBufferStride(stream);
            int offset = mesh.GetVertexAttributeOffset(VertexAttribute.Position);
            VertexAttributeFormat format = mesh.GetVertexAttributeFormat(VertexAttribute.Position);
            int size = ComponentSize(format);
            byte[] vertexBytes;
            using (GraphicsBuffer buffer = mesh.GetVertexBuffer(stream))
                vertexBytes = ReadBuffer(buffer);
            vertices = new Vector3[mesh.vertexCount];
            for (int index = 0; index < vertices.Length; ++index)
            {
                int position = checked(index * stride + offset);
                vertices[index] = new Vector3(
                    ReadComponent(vertexBytes, position, format),
                    ReadComponent(vertexBytes, position + size, format),
                    ReadComponent(vertexBytes, position + 2 * size, format));
            }

            byte[] indexBytes;
            using (GraphicsBuffer buffer = mesh.GetIndexBuffer())
                indexBytes = ReadBuffer(buffer);
            int indexSize = mesh.indexFormat == IndexFormat.UInt16 ? 2 : 4;
            var indices = new List<int>();
            for (int subMesh = 0; subMesh < mesh.subMeshCount; ++subMesh)
            {
                SubMeshDescriptor descriptor = mesh.GetSubMesh(subMesh);
                if (descriptor.topology != MeshTopology.Triangles) continue;
                if (descriptor.indexCount % 3 != 0)
                    throw new InvalidOperationException("Incomplete mesh triangle.");
                for (int index = 0; index < descriptor.indexCount; ++index)
                {
                    int position = checked((descriptor.indexStart + index) * indexSize);
                    int value = indexSize == 2 ? BitConverter.ToUInt16(indexBytes, position)
                        : checked((int)BitConverter.ToUInt32(indexBytes, position));
                    indices.Add(checked(value + descriptor.baseVertex));
                }
            }
            triangles = indices.ToArray();
        }

        private static byte[] ReadBuffer(GraphicsBuffer buffer)
        {
            if (buffer == null || !buffer.IsValid())
                throw new InvalidOperationException("Mesh GPU buffer is unavailable.");
            // ponytail: one blocking read per unique mesh when the editor opens;
            // batch asynchronously if measured opening time becomes a problem.
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(buffer);
            request.WaitForCompletion();
            if (request.hasError) throw new InvalidOperationException("Mesh GPU readback failed.");
            return request.GetData<byte>().ToArray();
        }

        private static int ComponentSize(VertexAttributeFormat format)
        {
            switch (format)
            {
                case VertexAttributeFormat.Float32:
                case VertexAttributeFormat.UInt32:
                case VertexAttributeFormat.SInt32: return 4;
                case VertexAttributeFormat.Float16:
                case VertexAttributeFormat.UNorm16:
                case VertexAttributeFormat.SNorm16:
                case VertexAttributeFormat.UInt16:
                case VertexAttributeFormat.SInt16: return 2;
                case VertexAttributeFormat.UNorm8:
                case VertexAttributeFormat.SNorm8:
                case VertexAttributeFormat.UInt8:
                case VertexAttributeFormat.SInt8: return 1;
                default: throw new NotSupportedException("Unsupported position format: " + format);
            }
        }

        private static float ReadComponent(byte[] bytes, int offset, VertexAttributeFormat format)
        {
            switch (format)
            {
                case VertexAttributeFormat.Float32: return BitConverter.ToSingle(bytes, offset);
                case VertexAttributeFormat.Float16:
                    int half = BitConverter.ToUInt16(bytes, offset);
                    int exponent = (half >> 10) & 31;
                    int fraction = half & 1023;
                    float value = exponent == 31 ? (fraction == 0 ? float.PositiveInfinity : float.NaN)
                        : exponent == 0 ? fraction * (float)Math.Pow(2, -24)
                        : (1024 + fraction) * (float)Math.Pow(2, exponent - 25);
                    return (half & 32768) == 0 ? value : -value;
                case VertexAttributeFormat.UNorm8: return bytes[offset] / 255f;
                case VertexAttributeFormat.SNorm8: return Mathf.Max(-1f, unchecked((sbyte)bytes[offset]) / 127f);
                case VertexAttributeFormat.UNorm16: return BitConverter.ToUInt16(bytes, offset) / 65535f;
                case VertexAttributeFormat.SNorm16: return Mathf.Max(-1f, BitConverter.ToInt16(bytes, offset) / 32767f);
                case VertexAttributeFormat.UInt8: return bytes[offset];
                case VertexAttributeFormat.SInt8: return unchecked((sbyte)bytes[offset]);
                case VertexAttributeFormat.UInt16: return BitConverter.ToUInt16(bytes, offset);
                case VertexAttributeFormat.SInt16: return BitConverter.ToInt16(bytes, offset);
                case VertexAttributeFormat.UInt32: return BitConverter.ToUInt32(bytes, offset);
                case VertexAttributeFormat.SInt32: return BitConverter.ToInt32(bytes, offset);
                default: throw new NotSupportedException("Unsupported position format: " + format);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
