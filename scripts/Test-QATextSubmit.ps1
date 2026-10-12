[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BuildWorksQaControl.cs'))
$start = $source.IndexOf('        private static void SubmitFocusedText(')
$end = $source.IndexOf('        private static void SetFocusedText(', $start)
if ($start -lt 0 -or $end -le $start) { throw 'QA text-submit method missing.' }
$method = $source.Substring($start, $end - $start)
# Execute the production decision with inert UI objects; real host dispatch still needs game proof.
$fixture = @'
using System;
namespace QaTextSubmitChecks {
public class Obj { public static implicit operator bool(Obj value) => value != null; }
public class GameObject : Obj {
    public TMP_InputField field;
    public T GetComponent<T>() where T:class => field as T;
}
public class TMP_InputField : Obj {
    public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
    public bool isActiveAndEnabled=true, isFocused=true, interactable=true, readOnly;
    public LineType lineType;
}
public class EventSystem : Obj {
    public static EventSystem current;
    public bool sendNavigationEvents;
    public GameObject currentSelectedGameObject;
}
public class BaseEventData { public EventSystem owner; public BaseEventData(EventSystem value){owner=value;} }
public static class ExecuteEvents {
    public static object submitHandler = new object();
    public static int count;
    public static void Execute(GameObject selected, BaseEventData data, object handler) {
        if (selected != EventSystem.current.currentSelectedGameObject || data.owner != EventSystem.current || handler != submitHandler)
            throw new Exception("Wrong native submit target/event");
        ++count;
    }
}
public static class BuildWorksQaControlProtocol { public class Step { public string[] keys; } }
public partial class Harness {
    public static string Run() {
        int checks=0;
        for(int variant=0;variant<14;variant++) {
            var field=new TMP_InputField();
            var selected=new GameObject{field=field};
            var events=new EventSystem{currentSelectedGameObject=selected}; EventSystem.current=events;
            var step=new BuildWorksQaControlProtocol.Step{keys=new[]{"Return"}};
            bool first=true;
            switch(variant) {
                case 1:first=false;break;
                case 2:step.keys=null;break;
                case 3:step.keys=new[]{"F5"};break;
                case 4:EventSystem.current=null;break;
                case 5:events.sendNavigationEvents=true;break;
                case 6:events.currentSelectedGameObject=null;break;
                case 7:selected.field=null;break;
                case 8:field.isActiveAndEnabled=false;break;
                case 9:field.isFocused=false;break;
                case 10:field.interactable=false;break;
                case 11:field.readOnly=true;break;
                case 12:field.lineType=TMP_InputField.LineType.MultiLineNewline;break;
                case 13:field.lineType=TMP_InputField.LineType.MultiLineSubmit;break;
            }
            ExecuteEvents.count=0;
            SubmitFocusedText(step,first);
            SubmitFocusedText(step,false); // A held Return must never submit again on later frames.
            if(ExecuteEvents.count != (variant==0 || variant==13 ? 1:0)) throw new Exception("Submit guard failed: "+variant);
            ++checks;
        }
        return "PASS "+checks+" production-method cases: one submit, held-key/no-key/null/focus/readonly/navigation/multiline guards. Inert UI only.";
    }
}
}
'@
$fixture += "`nnamespace QaTextSubmitChecks { public partial class Harness {`n" + $method + "`n} }"
Add-Type -TypeDefinition $fixture
[QaTextSubmitChecks.Harness]::Run()
$negative = $fixture.Replace('QaTextSubmitChecks', 'QaTextSubmitNegativeChecks').Replace(
    'ExecuteEvents.Execute(selected, new BaseEventData(events), ExecuteEvents.submitHandler);',
    '// Disconnected native submit, reproducing the old missing callback.')
Add-Type -TypeDefinition $negative
$rejected = $false
try { [QaTextSubmitNegativeChecks.Harness]::Run() | Out-Null }
catch {
    if ($_.Exception.GetBaseException().Message -ne 'Submit guard failed: 0') { throw }
    $rejected = $true
}
if (-not $rejected) { throw 'Regression accepted a disconnected native submit.' }
'PASS negative control: missing submit callback rejected in memory only.'
