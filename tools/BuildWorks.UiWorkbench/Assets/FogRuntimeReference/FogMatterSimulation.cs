using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildWorks.FogRuntimeReference
{
    // ponytail: one fixed wall and one actor; prescribed vortical forcing, no pressure solve or collision scene.
    public sealed class FogMatterSimulation : MonoBehaviour
    {
        public ComputeShader solver;
        public Texture3D shape, pigment, noise;
        public Vector3 minimum, maximum;
        public Vector3 restMinimum, restMaximum;
        public const float Step = 1f / 30;
        public const int Nx = 96, Ny = 80, Nz = 80;
        private readonly RenderTexture[] colour = new RenderTexture[2], velocity = new RenderTexture[2], origin = new RenderTexture[2], surface = new RenderTexture[2];
        private ComputeBuffer awayMass;
        private ComputeBuffer peakAway;
        private ComputeBuffer surfaceState;
        private AsyncGPUReadbackRequest surfaceReadback;
        private bool surfaceReadbackPending;
        public bool SurfaceActive { get; private set; }
        private RenderTexture predictedColour, predictedOrigin;
        private int current, tick, lastScenario = -1;
        private float lastSeed = float.NaN, inputTime;
        private Vector3 previous, inputPosition;
        private bool wasAutomatic;
        public RenderTexture Smoke => colour[current];
        public RenderTexture Flow => velocity[current];
        public RenderTexture Surface => surface[current];

        public static Vector3 Position(float time, int scenario)
        {
            float phase = Mathf.Repeat(time, 12);
            if (scenario >= 2)
                return new Vector3(-.12f, .95f, Mathf.Lerp(-2.1f, 2.1f, Mathf.Clamp01((phase - 2) / (scenario == 3 ? .6f : 3))));
            return new Vector3(Mathf.Lerp(-1.8f, 1.8f, Mathf.Clamp01((phase - 2) / (scenario == 1 ? .5f : 1.2f))), .95f, 0);
        }

        private RenderTexture Allocate(string label)
        {
            var field = new RenderTexture(Nx, Ny, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = label, dimension = TextureDimension.Tex3D, volumeDepth = Nz, enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave
            };
            if (!field.Create()) { DestroyImmediate(field); throw new InvalidOperationException("Cannot create smoke simulation field " + label); }
            return field;
        }

        public void ResetState(float time, Vector3 position, bool automatic, int scenario, float seed)
        {
            if (!solver || !shape || !pigment || !noise) throw new InvalidOperationException("Missing wall smoke simulation assets");
            if (!predictedColour) predictedColour = Allocate("Predicted smoke");
            if (!predictedOrigin) predictedOrigin = Allocate("Predicted origins");
            if (awayMass == null) awayMass = new ComputeBuffer(32*32,sizeof(uint));
            if (peakAway == null) peakAway = new ComputeBuffer(32*32,sizeof(uint));
            if(surfaceReadbackPending) { surfaceReadback.WaitForCompletion(); surfaceReadbackPending=false; }
            SurfaceActive=false;
            if(surfaceState == null) surfaceState=new ComputeBuffer(1,sizeof(uint));
            int clearLedger = solver.FindKernel("ClearLedger");
            solver.SetBuffer(clearLedger,"_AwayMass",awayMass); solver.SetBuffer(clearLedger,"_SurfaceState",surfaceState);
            solver.SetBuffer(clearLedger,"_PeakAway",peakAway); solver.SetInt("_ResetLedger",1);
            solver.Dispatch(clearLedger,16,1,1);
            solver.SetInt("_ResetLedger",0);
            for (int i = 0; i < 2; ++i)
            {
                if (!colour[i]) colour[i] = Allocate("Smoke colour and mass " + i);
                if (!velocity[i]) velocity[i] = Allocate("Velocity and wall depletion " + i);
                if (!origin[i]) origin[i] = Allocate("Original position and age " + i);
                if (!surface[i]) surface[i] = Allocate("Wall displacement and depletion " + i);
                int clear = solver.FindKernel("Clear");
                solver.SetTexture(clear, "_WriteColour", colour[i]); solver.SetTexture(clear, "_WriteVelocity", velocity[i]);
                solver.SetTexture(clear, "_WriteOrigin", origin[i]); solver.SetTexture(clear,"_WriteSurface",surface[i]);
                solver.Dispatch(clear, Nx / 8, Ny / 8, Nz / 4);
            }
            current = 0; RebaseControl(time,position,automatic,scenario,seed);
        }

        public void RebaseControl(float time, Vector3 position, bool automatic, int scenario, float seed)
        {
            tick = Mathf.FloorToInt(time / Step + .0001f);
            previous = inputPosition = position; inputTime = time;
            lastScenario = scenario; lastSeed = seed; wasAutomatic = automatic;
        }

        public void Evaluate(float time, Vector3 position, Vector2 body, bool automatic, int scenario, float seed, bool emit)
        {
            int target = Mathf.FloorToInt(time / Step + .0001f);
            if (!Smoke || target < tick || lastScenario != scenario || lastSeed != seed)
                ResetState(automatic ? 0 : time, automatic ? Position(0, scenario) : position, automatic, scenario, seed);
            else if (wasAutomatic != automatic) RebaseControl(inputTime,inputPosition,automatic,scenario,seed);
            solver.SetVector("_Minimum", minimum); solver.SetVector("_Maximum", maximum);
            solver.SetVector("_RestMin",restMinimum); solver.SetVector("_RestMax",restMaximum);
            solver.SetVector("_Body", body); solver.SetFloat("_Seed", seed); solver.SetFloat("_Dt", Step);
            bool advanced=tick<target;
            while (tick < target)
            {
                float nextTime = (++tick) * Step;
                Vector3 next = automatic ? Position(nextTime, scenario) : Vector3.Lerp(inputPosition, position,
                    Mathf.InverseLerp(inputTime, time, nextTime));
                solver.SetFloat("_Clock", nextTime); solver.SetFloat("_Emit", emit ? 1 : 0);
                solver.SetVector("_Actor", next); solver.SetVector("_ActorVelocity", (next - previous) / Step);
                int nextIndex = 1 - current;
                int flow = solver.FindKernel("AdvanceVelocity"), matter = solver.FindKernel("AdvanceMatter"), correct = solver.FindKernel("CorrectMatter");
                foreach (int kernel in new[] { flow, matter, correct })
                {
                    solver.SetTexture(kernel, "_ReadColour", colour[current]); solver.SetTexture(kernel, "_ReadVelocity", velocity[current]);
                    solver.SetTexture(kernel, "_ReadOrigin", origin[current]); solver.SetTexture(kernel, "_Shape", shape);
                    solver.SetTexture(kernel, "_Pigment", pigment); solver.SetTexture(kernel, "_Noise", noise);
                    solver.SetTexture(kernel,"_ReadSurface",surface[current]);
                }
                solver.SetBuffer(flow,"_AwayMass",awayMass);
                solver.SetBuffer(flow,"_PeakAway",peakAway);
                solver.SetTexture(flow, "_WriteVelocity", velocity[nextIndex]);
                solver.Dispatch(flow, Nx / 8, Ny / 8, Nz / 4);
                solver.SetTexture(matter, "_Velocity", velocity[nextIndex]);
                solver.SetTexture(matter, "_WriteColour", predictedColour); solver.SetTexture(matter, "_WriteOrigin", predictedOrigin);
                solver.Dispatch(matter, Nx / 8, Ny / 8, Nz / 4);
                solver.SetTexture(correct, "_Velocity", velocity[nextIndex]);
                solver.SetTexture(correct, "_PredictedColour", predictedColour); solver.SetTexture(correct, "_PredictedOrigin", predictedOrigin);
                solver.SetTexture(correct, "_WriteColour", colour[nextIndex]); solver.SetTexture(correct, "_WriteOrigin", origin[nextIndex]);
                solver.Dispatch(correct, Nx / 8, Ny / 8, Nz / 4);
                int clearLedger = solver.FindKernel("ClearLedger"), ledger = solver.FindKernel("AccumulateLedger"), restore = solver.FindKernel("UpdateSurface");
                solver.SetBuffer(clearLedger,"_AwayMass",awayMass); solver.SetBuffer(clearLedger,"_SurfaceState",surfaceState);
                solver.SetBuffer(clearLedger,"_PeakAway",peakAway);
                solver.Dispatch(clearLedger,16,1,1);
                solver.SetBuffer(ledger,"_AwayMass",awayMass);
                solver.SetTexture(ledger,"_ReadColour",colour[nextIndex]); solver.SetTexture(ledger,"_ReadOrigin",origin[nextIndex]);
                solver.Dispatch(ledger,Nx/8,Ny/8,Nz/4);
                solver.SetTexture(restore,"_ReadSurface",surface[current]); solver.SetTexture(restore,"_Velocity",velocity[nextIndex]);
                solver.SetBuffer(restore,"_SurfaceState",surfaceState);
                solver.SetTexture(restore,"_WriteSurface",surface[nextIndex]); solver.Dispatch(restore,Nx/8,Ny/8,Nz/4);
                current = nextIndex; previous = next;
            }
            inputTime = time; inputPosition = position;
            if(surfaceReadbackPending && surfaceReadback.done)
            {
                if(surfaceReadback.hasError) throw new InvalidOperationException("Wall surface state readback failed");
                SurfaceActive=surfaceReadback.GetData<uint>()[0]!=0; surfaceReadbackPending=false;
            }
            if(advanced && !surfaceReadbackPending)
            {
                surfaceReadback=AsyncGPUReadback.Request(surfaceState); surfaceReadbackPending=true;
                // Capture steps must observe the exact current mesh; interactive playback reads asynchronously.
                if(Application.isBatchMode)
                {
                    surfaceReadback.WaitForCompletion();
                    if(surfaceReadback.hasError) throw new InvalidOperationException("Wall surface state readback failed");
                    SurfaceActive=surfaceReadback.GetData<uint>()[0]!=0; surfaceReadbackPending=false;
                }
            }
        }

        private void OnDisable()
        {
            if(surfaceReadbackPending) { surfaceReadback.WaitForCompletion(); surfaceReadbackPending=false; }
            foreach (RenderTexture field in new[] { predictedColour, predictedOrigin })
                if(field) { field.Release(); DestroyImmediate(field); }
            predictedColour=null;predictedOrigin=null;
            if(awayMass != null) { awayMass.Release(); awayMass=null; }
            if(peakAway != null) { peakAway.Release(); peakAway=null; }
            if(surfaceState != null) { surfaceState.Release(); surfaceState=null; }
            SurfaceActive=false;
            foreach (RenderTexture[] pair in new[] { colour, velocity, origin, surface })
                for (int i = 0; i < pair.Length; ++i)
                    if (pair[i]) { pair[i].Release(); DestroyImmediate(pair[i]); pair[i] = null; }
        }
    }
}
