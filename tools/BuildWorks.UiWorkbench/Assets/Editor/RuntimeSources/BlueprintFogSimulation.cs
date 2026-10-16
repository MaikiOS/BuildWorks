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
        private bool disposed;
        internal RenderTexture Smoke => colour[current];
        internal RenderTexture Flow => velocity[current];
        internal RenderTexture Surface => surface[current];
        internal int Tick => tick;

        internal BlueprintFogSimulation(BlueprintFogAssets assets, Texture3D shape, Texture3D pigment,
            Texture3D noise, Vector3 minimum, Vector3 maximum, Vector3 restMinimum, Vector3 restMaximum,
            Vector3 actor, float seed)
        {
            Validate(actor); Validate(minimum); Validate(maximum); Validate(restMinimum); Validate(restMaximum);
            if (assets == null || !assets.Solver || !shape || !pigment || !noise || !Finite(seed))
                throw new ArgumentException("Missing fog simulation input.");
            for (int axis = 0; axis < 3; ++axis)
                if (maximum[axis] <= minimum[axis] || restMaximum[axis] <= restMinimum[axis] ||
                    minimum[axis] > restMinimum[axis] || maximum[axis] < restMaximum[axis] ||
                    Mathf.Abs(minimum[axis]) > 64 || Mathf.Abs(maximum[axis]) > 64)
                    throw new ArgumentException("Fog simulation requires bounded local coordinates enclosing the rest shape.");
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                throw new NotSupportedException("Fog simulation half-float fields unavailable.");
            try
            {
                solver = UnityEngine.Object.Instantiate(assets.Solver);
                solver.hideFlags = HideFlags.HideAndDontSave;
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
                foreach (int kernel in new[] { clearLedger, restore }) solver.SetBuffer(kernel, "_SurfaceState", state);
                solver.SetInt("_ResetLedger", 1); solver.Dispatch(clearLedger, 16, 1, 1); solver.SetInt("_ResetLedger", 0);
                predictedColour = Allocate("Predicted smoke"); predictedOrigin = Allocate("Predicted origins");
                for (int index = 0; index < 2; ++index)
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

        internal void AdvanceTo(float time, Vector3 actor, Vector2 body, bool emit)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogSimulation));
            Validate(actor);
            if (!Finite(time) || time < inputTime || time > 86400 || !Finite(body.x) || !Finite(body.y) ||
                body.x <= 0 || body.y < body.x || body.y > 8)
                throw new ArgumentException("Invalid fog clock or body capsule.");
            int target = Mathf.FloorToInt(time / Step + .0001f);
            // Reject an unbounded catch-up rather than stalling the game or silently resetting visible matter.
            if (target - tick > 8) throw new ArgumentException("Fog advance exceeds eight fixed steps; caller must bound elapsed time.");
            solver.SetVector("_Body", body); solver.SetFloat("_Emit", emit ? 1 : 0);
            while (tick < target)
            {
                float nextTime = (++tick) * Step;
                Vector3 next = Vector3.Lerp(inputPosition, actor, Mathf.InverseLerp(inputTime, time, nextTime));
                solver.SetFloat("_Clock", nextTime); solver.SetVector("_Actor", next);
                solver.SetVector("_ActorVelocity", (next - previous) / Step);
                int nextIndex = 1 - current;
                foreach (int kernel in transport)
                {
                    solver.SetTexture(kernel, "_ReadColour", colour[current]); solver.SetTexture(kernel, "_ReadVelocity", velocity[current]);
                    solver.SetTexture(kernel, "_ReadOrigin", origin[current]); solver.SetTexture(kernel, "_ReadSurface", surface[current]);
                }
                solver.SetTexture(flow, "_WriteVelocity", velocity[nextIndex]); Dispatch(flow);
                solver.SetTexture(matter, "_Velocity", velocity[nextIndex]); solver.SetTexture(matter, "_WriteColour", predictedColour);
                solver.SetTexture(matter, "_WriteOrigin", predictedOrigin); Dispatch(matter);
                solver.SetTexture(correct, "_Velocity", velocity[nextIndex]); solver.SetTexture(correct, "_PredictedColour", predictedColour);
                solver.SetTexture(correct, "_PredictedOrigin", predictedOrigin); solver.SetTexture(correct, "_WriteColour", colour[nextIndex]);
                solver.SetTexture(correct, "_WriteOrigin", origin[nextIndex]); Dispatch(correct);
                solver.Dispatch(clearLedger, 16, 1, 1);
                solver.SetTexture(ledger, "_ReadColour", colour[nextIndex]); solver.SetTexture(ledger, "_ReadOrigin", origin[nextIndex]); Dispatch(ledger);
                solver.SetTexture(restore, "_ReadSurface", surface[current]); solver.SetTexture(restore, "_Velocity", velocity[nextIndex]);
                solver.SetTexture(restore, "_WriteSurface", surface[nextIndex]); Dispatch(restore);
                current = nextIndex; previous = next;
            }
            inputTime = time; inputPosition = actor;
        }

        private void Dispatch(int kernel) => solver.Dispatch(kernel, Nx / 8, Ny / 8, Nz / 4);

        private static RenderTexture Allocate(string label)
        {
            var field = new RenderTexture(Nx, Ny, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) {
                name = "BuildWorks fog " + label, dimension = TextureDimension.Tex3D, volumeDepth = Nz,
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
