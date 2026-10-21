using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

public static partial class FogProductionFramesCheck
{
    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int D3DDisassemble(byte[] data, UIntPtr length, uint flags, string comments, out IntPtr blob);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr BlobPointer(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate UIntPtr BlobSize(IntPtr self);

    // Editor source compilation, not extraction of the installed bundle or GPU execution timing.
    public static void RunShaderCompilerProbe()
    {
        string output = Argument("-fogEvidenceOutput"); Require(!string.IsNullOrEmpty(output), "Compiler output required.");
        var report = new CompilerReport();
        try
        {
            Require(Application.platform == RuntimePlatform.WindowsEditor, "DXBC probe is Windows Editor only.");
            IntPtr invalid = IntPtr.Zero;
            try { report.invalidContainerRejected = D3DDisassemble(new byte[4], (UIntPtr)4, 0, null, out invalid) < 0; }
            finally { if (invalid != IntPtr.Zero) Marshal.Release(invalid); }
            Require(report.invalidContainerRejected, "D3DDisassemble accepted an invalid container.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/BuildWorksFogRuntime/FogGhostVolume.shader");
            Require(shader != null, "Fog source shader missing.");
            var pass = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(0);
            Require(pass != null && pass.HasShaderStage(ShaderType.Fragment), "Fog fragment pass missing.");
            foreach (bool baseline in new[] { false, true })
            {
                var keywords = new List<string> { "BUILDWORKS_FOG_ATLAS", "BUILDWORKS_FOG_PUBLISHED", "BUILDWORKS_FOG_DORMANT_FAST" };
                if (baseline) keywords.Add("BUILDWORKS_FOG_BOUNDS_BASELINE");
                var result = pass.CompileVariant(ShaderType.Fragment, keywords.ToArray(), ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64, true);
                Require(result.Success && result.Messages.Length == 0 && result.ShaderData != null && result.ShaderData.Length > 4,
                    "Fragment variant failed or has compiler messages/empty data.");
                byte[] bytes = result.ShaderData;
                Require(Encoding.ASCII.GetString(bytes, 0, 4) == "DXBC", "External compiler output is not a DXBC container; no header guessing allowed.");
                string name = baseline ? "bounds-baseline" : "published";
                File.WriteAllBytes(Path.Combine(output, name + ".dxbc"), bytes);
                IntPtr blob = IntPtr.Zero;
                try
                {
                    int hr = D3DDisassemble(bytes, (UIntPtr)bytes.Length, 0, null, out blob);
                    Require(hr >= 0 && blob != IntPtr.Zero, "D3DDisassemble failed: " + hr);
                    IntPtr table = Marshal.ReadIntPtr(blob);
                    // ID3DBlob inherits IUnknown; its two following methods expose pointer and SIZE_T.
                    var pointer = Marshal.GetDelegateForFunctionPointer<BlobPointer>(Marshal.ReadIntPtr(table, 3 * IntPtr.Size));
                    var size = Marshal.GetDelegateForFunctionPointer<BlobSize>(Marshal.ReadIntPtr(table, 4 * IntPtr.Size));
                    ulong count = size(blob).ToUInt64(); Require(count > 0 && count <= 16 * 1024 * 1024, "Disassembly size outside bounds.");
                    string assembly = Marshal.PtrToStringAnsi(pointer(blob), checked((int)count)).TrimEnd('\0');
                    Require(assembly.Contains("ps_5_0"), "Unexpected fragment shader model.");
                    File.WriteAllText(Path.Combine(output, name + ".asm"), assembly);
                    using (var hash = SHA256.Create()) report.variants.Add(new CompilerVariant { name = name, bytes = bytes.Length,
                        sha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""),
                        declarations = assembly.Split('\n').Where(v => v.TrimStart().StartsWith("dcl_temps ") || v.TrimStart().StartsWith("dcl_indexableTemp ")).ToArray() });
                }
                finally { if (blob != IntPtr.Zero) Marshal.Release(blob); }
            }
            report.passed = report.variants.Count == 2;
        }
        catch (Exception error) { report.failure = error.ToString(); Debug.LogException(error); }
        File.WriteAllText(Path.Combine(output, "compiler-verification.json"), JsonUtility.ToJson(report, true));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
    [Serializable] private sealed class CompilerReport
    {
        public string scope = "Editor source fragment compile/disassembly only; not installed bundle bytes, hardware registers or execution timings", failure;
        public bool passed, invalidContainerRejected;
        public List<CompilerVariant> variants = new List<CompilerVariant>();
    }
    [Serializable] private sealed class CompilerVariant { public string name, sha256; public int bytes; public string[] declarations; }
}
