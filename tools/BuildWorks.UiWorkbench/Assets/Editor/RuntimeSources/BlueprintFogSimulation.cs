using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    // shortcut: approved one-wall YZ ownership; extend ownership before sharing a field across a house.
    // Coordinates are local metres; shape/pigment/noise are borrowed and must outlive this simulation.
    internal sealed class BlueprintFogSimulation : IDisposable
    {
        internal const int Nx = 96, Ny = 80, Nz = 80;
        internal const float Step = 1f / 30;
        internal const long FieldBytes = (long)Nx * Ny * Nz * 8 * 10;
        internal Vector3Int GridSize { get; private set; }
        internal long AllocatedFieldBytes => FieldVoxels(GridSize) * (Scratch == null ? 80 : 32);
        internal BlueprintFogScratch Scratch { get; }
        private readonly RenderTexture[] colour = new RenderTexture[2], velocity = new RenderTexture[2],
            origin = new RenderTexture[2], surface = new RenderTexture[2];
        private RenderTexture predictedColour, predictedOrigin;
        private ComputeShader solver;
        private ComputeBuffer away, peak, state;
        private readonly int clear, flow, matter, correct, clearLedger, ledger, restore;
        private readonly int[] transport;
        private int current, tick;
        private float inputTime;
        private Vector3 previous, inputPosition;
        private bool disposed, faulted;
        private AsyncGPUReadbackRequest recoveryRequest;
        private bool recoveryPending, observedRecovery;
        private int emissionRevision, requestedRevision, observedRevision;
        internal RenderTexture Smoke => colour[current];
        internal RenderTexture Flow => velocity[current];
        internal RenderTexture Surface => surface[current];
        internal int Tick => tick;

        internal BlueprintFogSimulation(BlueprintFogAssets assets, Texture3D shape, Texture3D pigment,
            Texture3D noise, Vector3 minimum, Vector3 maximum, Vector3 restMinimum, Vector3 restMaximum,
            Vector3 actor, float seed, Vector3Int? gridSize = null, float initialClock = 0, BlueprintFogScratch scratch = null)
        {
            if (!Finite(initialClock) || initialClock < 0 || initialClock > 86400) throw new ArgumentException("Invalid initial fog clock.");
            tick = Mathf.FloorToInt(initialClock / Step + .0001f); inputTime = initialClock;
            Validate(actor); Validate(minimum); Validate(maximum); Validate(restMinimum); Validate(restMaximum);
            if (assets == null || !assets.Solver || !shape || !pigment || !noise || !Finite(seed))
                throw new ArgumentException("Missing fog simulation input.");
            for (int axis = 0; axis < 3; ++axis)
                if (maximum[axis] <= minimum[axis] || restMaximum[axis] <= restMinimum[axis] ||
                    minimum[axis] > restMinimum[axis] || maximum[axis] < restMaximum[axis] ||
                    Mathf.Abs(minimum[axis]) > 64 || Mathf.Abs(maximum[axis]) > 64)
                    throw new ArgumentException("Fog simulation requires bounded local coordinates enclosing the rest shape.");
            GridSize = gridSize ?? new Vector3Int(Nx, Ny, Nz);
            ValidateGrid(GridSize, maximum - minimum, gridSize.HasValue);
            if (scratch != null && scratch.GridSize != GridSize) throw new ArgumentException("Fog scratch must match the exact simulation grid.");
            Scratch = scratch;
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                throw new NotSupportedException("Fog simulation half-float fields unavailable.");
            if (Scratch != null && (SystemInfo.copyTextureSupport & CopyTextureSupport.Copy3D) == 0)
                throw new NotSupportedException("Shared fog scratch requires native 3D texture copying.");
            try
            {
                solver = UnityEngine.Object.Instantiate(assets.Solver);
                solver.hideFlags = HideFlags.HideAndDontSave;
                if (gridSize.HasValue)
                {
                    solver.EnableKeyword("BUILDWORKS_FOG_COMPACT_GRID");
                    solver.SetVector("_GridResolution", new Vector4(GridSize.x, GridSize.y, GridSize.z, 0));
                }
                else solver.DisableKeyword("BUILDWORKS_FOG_COMPACT_GRID");
                clear = solver.FindKernel("Clear"); flow = solver.FindKernel("AdvanceVelocity");
                matter = solver.FindKernel("AdvanceMatter"); correct = solver.FindKernel("CorrectMatter");
                clearLedger = solver.FindKernel("ClearLedger"); ledger = solver.FindKernel("AccumulateLedger");
                restore = solver.FindKernel("UpdateSurface"); transport = new[] { flow, matter, correct };
                solver.SetVector("_Minimum", minimum); solver.SetVector("_Maximum", maximum);
                solver.SetVector("_RestMin", restMinimum); solver.SetVector("_RestMax", restMaximum);
                solver.SetFloat("_Seed", seed); solver.SetFloat("_Dt", Step);
                foreach (int kernel in transport)
                {
                    solver.SetTexture(kernel, "_Shape", shape); solver.SetTexture(kernel, "_Pigment", pigment);
                    solver.SetTexture(kernel, "_Noise", noise);
                }
                away = new ComputeBuffer(1024, sizeof(uint)); peak = new ComputeBuffer(1024, sizeof(uint));
                state = new ComputeBuffer(1, sizeof(uint));
                foreach (int kernel in new[] { clearLedger, flow, ledger }) solver.SetBuffer(kernel, "_AwayMass", away);
                foreach (int kernel in new[] { clearLedger, flow }) solver.SetBuffer(kernel, "_PeakAway", peak);
                foreach (int kernel in new[] { clearLedger, ledger, restore }) solver.SetBuffer(kernel, "_SurfaceState", state);
                solver.SetInt("_ResetLedger", 1); solver.Dispatch(clearLedger, 16, 1, 1); solver.SetInt("_ResetLedger", 0);
                if (Scratch == null) { predictedColour = Allocate("Predicted smoke"); predictedOrigin = Allocate("Predicted origins"); }
                for (int index = 0; index < (Scratch == null ? 2 : 1); ++index)
                {
                    colour[index] = Allocate("Smoke " + index); velocity[index] = Allocate("Velocity " + index);
                    origin[index] = Allocate("Origins " + index); surface[index] = Allocate("Surface " + index);
                    solver.SetTexture(clear, "_WriteColour", colour[index]); solver.SetTexture(clear, "_WriteVelocity", velocity[index]);
                    solver.SetTexture(clear, "_WriteOrigin", origin[index]); solver.SetTexture(clear, "_WriteSurface", surface[index]);
                    Dispatch(clear);
                }
                previous = inputPosition = actor;
            }
            catch { Dispose(); throw; }
        }

        internal void AdvanceTo(float time, Vector3 actor, Vector2 body, bool emit, bool emissionPossible = true)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogSimulation));
            if (faulted) throw new InvalidOperationException("Fog step result is uncertain; automatic retry is blocked.");
            Validate(actor);
            if (!Finite(time) || time < inputTime || time > 86400 || !Finite(body.x) || !Finite(body.y) ||
                body.x <= 0 || body.y < body.x || body.y > 8)
                throw new ArgumentException("Invalid fog clock or body capsule.");
            int target = Mathf.FloorToInt(time / Step + .0001f);
            // Reject an unbounded catch-up rather than stalling the game or silently resetting visible matter.
            if (target - tick > 8) throw new ArgumentException("Fog advance exceeds eight fixed steps; caller must bound elapsed time.");
            // Body wind can continue outside the source without invalidating a recovery readback.
            if (emit && emissionPossible && target > tick) ++emissionRevision;
            solver.SetVector("_Body", body); solver.SetFloat("_Emit", emit ? 1 : 0);
            while (tick < target)
            {
                float nextTime = (++tick) * Step;
                Vector3 next = Vector3.Lerp(inputPosition, actor, Mathf.InverseLerp(inputTime, time, nextTime));
                solver.SetFloat("_Clock", nextTime); solver.SetVector("_Actor", next);
                solver.SetVector("_ActorVelocity", (next - previous) / Step);
                int nextIndex = 1 - current;
                RenderTexture nextColour = Scratch?.Colour ?? colour[nextIndex], nextVelocity = Scratch?.Velocity ?? velocity[nextIndex],
                    nextOrigin = Scratch?.Origin ?? origin[nextIndex], nextSurface = Scratch?.Surface ?? surface[nextIndex];
                RenderTexture prediction = Scratch?.PredictedColour ?? predictedColour, predictionOrigin = Scratch?.PredictedOrigin ?? predictedOrigin;
                try
                {
                    foreach (int kernel in transport)
                    {
                        solver.SetTexture(kernel, "_ReadColour", colour[current]); solver.SetTexture(kernel, "_ReadVelocity", velocity[current]);
                        solver.SetTexture(kernel, "_ReadOrigin", origin[current]); solver.SetTexture(kernel, "_ReadSurface", surface[current]);
                    }
                    solver.SetTexture(flow, "_WriteVelocity", nextVelocity); Dispatch(flow);
                    solver.SetTexture(matter, "_Velocity", nextVelocity); solver.SetTexture(matter, "_WriteColour", prediction);
                    solver.SetTexture(matter, "_WriteOrigin", predictionOrigin); Dispatch(matter);
                    solver.SetTexture(correct, "_Velocity", nextVelocity); solver.SetTexture(correct, "_PredictedColour", prediction);
                    solver.SetTexture(correct, "_PredictedOrigin", predictionOrigin); solver.SetTexture(correct, "_WriteColour", nextColour);
                    solver.SetTexture(correct, "_WriteOrigin", nextOrigin); Dispatch(correct);
                    solver.Dispatch(clearLedger, 16, 1, 1);
                    solver.SetTexture(ledger, "_ReadColour", nextColour); solver.SetTexture(ledger, "_ReadOrigin", nextOrigin); Dispatch(ledger);
                    solver.SetTexture(restore, "_ReadSurface", surface[current]); solver.SetTexture(restore, "_Velocity", nextVelocity);
                    solver.SetTexture(restore, "_WriteSurface", nextSurface); Dispatch(restore);
                    if (Scratch != null)
                    {
                        // Finish every read of the old state before copying any result back to its owner.
                        Graphics.CopyTexture(nextColour, colour[0]); Graphics.CopyTexture(nextVelocity, velocity[0]);
                        Graphics.CopyTexture(nextOrigin, origin[0]); Graphics.CopyTexture(nextSurface, surface[0]);
                    }
                    else current = nextIndex;
                    previous = next;
                }
                catch { faulted = true; throw; }
            }
            inputTime = time; inputPosition = actor;
        }

        internal bool TryGetRecovered()
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogSimulation));
            if (faulted) throw new InvalidOperationException("Uncertain fog step cannot authorize recovery.");
            if (recoveryPending && recoveryRequest.done)
            {
                recoveryPending = false;
                if (recoveryRequest.hasError) throw new InvalidOperationException("Fog recovery readback failed; retain its owner.");
                var flags = recoveryRequest.GetData<uint>();
                if (flags.Length != 1) throw new InvalidOperationException("Incomplete fog recovery state.");
                observedRecovery = flags[0] == 0; observedRevision = requestedRevision;
            }
            if (observedRecovery && observedRevision == emissionRevision) return true;
            if (!recoveryPending)
            {
                requestedRevision = emissionRevision;
                recoveryRequest = AsyncGPUReadback.Request(state); recoveryPending = true;
            }
            return false;
        }

        // Preserve the approved physical voxel spacing when allocating a smaller local region.
        internal static Vector3Int CompactGrid(Vector3 minimum, Vector3 maximum)
        {
            Validate(minimum); Validate(maximum);
            Vector3 size = maximum - minimum;
            int Round(float value, int group) => Mathf.CeilToInt(value / group - .00001f) * group;
            var grid = new Vector3Int(Mathf.Max(8, Round(size.x / .075f, 8)),
                Mathf.Max(8, Round(size.y / .06375f, 8)), Mathf.Max(4, Round(size.z / .0675f, 4)));
            ValidateGrid(grid, size, true); return grid;
        }

        private static void ValidateGrid(Vector3Int grid, Vector3 size, bool compact)
        {
            if (size.x <= 0 || size.y <= 0 || size.z <= 0)
                throw new ArgumentException("Fog grid exceeds its bounded allocation/dispatch budget.");
            FieldVoxels(grid);
            if (compact && (size.x / grid.x > .07501f || size.y / grid.y > .06376f || size.z / grid.z > .06751f))
                throw new ArgumentException("Fog compact grid cannot reduce the approved voxel detail.");
        }

        private void Dispatch(int kernel) => solver.Dispatch(kernel, GridSize.x / 8, GridSize.y / 8, GridSize.z / 4);

        private RenderTexture Allocate(string label)
            => AllocateField(GridSize, label);

        internal static long FieldVoxels(Vector3Int grid)
        {
            if (grid.x < 8 || grid.y < 8 || grid.z < 4 || grid.x > 256 || grid.y > 256 || grid.z > 256 ||
                grid.x % 8 != 0 || grid.y % 8 != 0 || grid.z % 4 != 0 || (long)grid.x * grid.y * grid.z > (long)Nx * Ny * Nz)
                throw new ArgumentException("Fog grid exceeds its bounded allocation/dispatch budget.");
            return (long)grid.x * grid.y * grid.z;
        }

        internal static RenderTexture AllocateField(Vector3Int grid, string label)
        {
            FieldVoxels(grid);
            var field = new RenderTexture(grid.x, grid.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) {
                name = "BuildWorks fog " + label, dimension = TextureDimension.Tex3D, volumeDepth = grid.z,
                enableRandomWrite = true, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!field.Create()) { UnityEngine.Object.DestroyImmediate(field); throw new InvalidOperationException("Cannot allocate fog field: " + label); }
            return field;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (recoveryPending) { recoveryRequest.WaitForCompletion(); recoveryPending = false; }
            Release(ref predictedColour); Release(ref predictedOrigin);
            foreach (RenderTexture[] pair in new[] { colour, velocity, origin, surface })
                for (int index = 0; index < pair.Length; ++index) Release(ref pair[index]);
            away?.Release(); away = null; peak?.Release(); peak = null; state?.Release(); state = null;
            if (solver) UnityEngine.Object.DestroyImmediate(solver);
            solver = null;
        }

        private static void Release(ref RenderTexture field)
        { if (field) { field.Release(); UnityEngine.Object.DestroyImmediate(field); } field = null; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Validate(Vector3 value)
        {
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z) ||
                Mathf.Abs(value.x) > 64 || Mathf.Abs(value.y) > 64 || Mathf.Abs(value.z) > 64)
                throw new ArgumentException("Fog coordinates must be finite local metres within the field budget.");
        }
    }
}
