#if BUILDWORKS_TEST_STAND || BUILDWORKS_QA_CONTROL_TESTS
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksQaControlProtocol
    {
        internal const int MaximumBytes = 32768;
#pragma warning disable CS0649 // Fields are populated by the bounded DataContract reader.

        [DataContract]
        internal sealed class Request
        {
            [DataMember(IsRequired = true)] public int version;
            [DataMember(IsRequired = true)] public string nonce;
            [DataMember(IsRequired = true)] public long id;
            [DataMember(IsRequired = true)] public long expiresUtc;
            [DataMember(IsRequired = true)] public string action;
            [DataMember] public bool capture;
            [DataMember] public Step[] steps;
            [DataMember] public long? stopRevision;
        }

        [DataContract]
        internal sealed class Step
        {
            [DataMember(IsRequired = true)] public int milliseconds;
            [DataMember] public string[] keys;
            [DataMember] public int buttons;
            [DataMember] public bool pointer;
            [DataMember] public float x;
            [DataMember] public float y;
            [DataMember] public float lookX;
            [DataMember] public float lookY;
            [DataMember] public float scroll;
            [DataMember] public string text;
            [DataMember] public string uiTarget;
        }

#pragma warning restore CS0649
        internal static Request Read(byte[] bytes, string nonce, long lastId, long nowUtc, long stopRevision = 0)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes)
                throw new InvalidDataException("QA request size must be 1..32768 bytes.");
            Request request;
            using (var stream = new MemoryStream(bytes, false))
                request = (Request)new DataContractJsonSerializer(typeof(Request),
                    new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 1024 }).ReadObject(stream);
            if (request == null || request.version != 1 || request.nonce != nonce || request.id <= lastId ||
                request.id <= 0 || request.expiresUtc < nowUtc || request.expiresUtc - nowUtc > 30000)
                throw new InvalidDataException("QA version, session, ID or expiry is invalid.");
            if (request.action != "batch" && request.action != "state" && request.action != "stop")
                throw new InvalidDataException("Unknown QA action.");
            if (request.action == "batch" && request.stopRevision.HasValue && request.stopRevision.Value != stopRevision)
                throw new InvalidDataException("QA input was stopped after this decision; do not resume automatically.");
            if (request.action != "batch")
            {
                if (request.steps != null && request.steps.Length != 0) throw new InvalidDataException("Only batch accepts steps.");
                return request;
            }
            if (request.steps == null || request.steps.Length < 1 || request.steps.Length > 32)
                throw new InvalidDataException("QA batch needs 1..32 steps.");
            int duration = 0;
            foreach (Step step in request.steps)
            {
                if (step == null || step.milliseconds < 20 || step.milliseconds > 2000 ||
                    step.buttons < 0 || step.buttons > 7 || !Finite(step.x) || !Finite(step.y) ||
                    !Finite(step.lookX) || !Finite(step.lookY) || !Finite(step.scroll) ||
                    step.pointer && (step.x < 0 || step.x > 1 || step.y < 0 || step.y > 1) ||
                    Math.Abs(step.lookX) > 2000 || Math.Abs(step.lookY) > 2000 || Math.Abs(step.scroll) > 5 ||
                    step.text != null && step.text.Length > 256 || step.keys != null &&
                    (step.keys.Length > 16 || step.keys.Any(key => string.IsNullOrEmpty(key) || key.Length > 20 ||
                        key.Any(character => !char.IsLetterOrDigit(character)))))
                    throw new InvalidDataException("Invalid QA input step.");
                duration += step.milliseconds;
            }
            if (duration > 10000) throw new InvalidDataException("QA batch exceeds 10 seconds.");
            if (request.steps.Any(step => step.uiTarget != null))
            {
                var first = request.steps[0];
                var release = request.steps.Length == 2 ? request.steps[1] : null;
                bool click = first.pointer && first.buttons == 1 && first.text == null && (first.keys == null || first.keys.Length == 0);
                bool text = !first.pointer && first.buttons == 0 && first.text != null &&
                    (first.keys == null || first.keys.Length == 0 || first.keys.Length == 1 && first.keys[0] == "Return");
                if (first.uiTarget == null || first.uiTarget.Length != 32 || first.uiTarget.Any(c => !Uri.IsHexDigit(c)) ||
                    !request.stopRevision.HasValue || (!click && !text) || first.lookX != 0 || first.lookY != 0 || first.scroll != 0 ||
                    release == null || release.uiTarget != null || release.pointer || release.buttons != 0 || release.text != null ||
                    release.lookX != 0 || release.lookY != 0 || release.scroll != 0 || release.keys != null && release.keys.Length != 0)
                    throw new InvalidDataException("Guarded QA UI input needs one target click/text and one neutral release with stopRevision.");
            }
            return request;
        }

        internal static bool CanContinue(Request request, long nowUtc, double elapsedMilliseconds) =>
            request != null && nowUtc <= request.expiresUtc && elapsedMilliseconds >= 0 && elapsedMilliseconds < 10000;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
#endif
