using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static partial class ControllerInputAcceptance
{
    internal static IEnumerable<object> RunKgExports(Camera template, TMP_Text font, string output)
    {
        string working=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/kg319",Guid.NewGuid().ToString("N").Substring(0,8)));
        var store=new CompositeBlueprintStore(Path.Combine(working,"native.json"));
        var blueprint=ExternalBlueprintLibrary.Parse("Name: edited KG\nAuthor: Original\nDescription: Source description\nObjects: [{Id: wood_wall}]\n","source.yml");
        var binary=ExternalBlueprintLibrary.LoadFile(Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../../artifacts/point-2.14-20261028/fresh-capture/capture-c17ea102/original-kg-capture.oprint"))).Single();
        Require(binary.importError==null,"Actual binary UI fixture refused: "+binary.importError);
        var prefab=GameObject.CreatePrimitive(PrimitiveType.Cube);prefab.SetActive(false);
        var input=new InputFrames();
        try
        {
            using(var controller=new BlueprintEditorController(store,template,font,_=>prefab,id=>id,
                ()=>Array.Empty<BlueprintEditorCatalogItem>(),error=>{throw new InvalidOperationException(error);},editorInput:input))
            {
                Require(controller.OpenNew(out string error),error);
                var view=Field<BlueprintEditorView>(controller,"view");
                foreach(int mode in Enumerable.Range(0,4))
                {
                    bool russian=mode%2==1, binaryFormat=mode>=2;
                    var selected=binaryFormat?binary:blueprint;
                    string extension=binaryFormat?".oprint":".yml";
                    BuildWorksLocalization.RussianLanguage=russian;
                    view.ShowExchange((parent,textFont,closed)=>new BlueprintExchangeView(parent,textFont,store,selected,closed));yield return null;
                    var exchange=Field<BlueprintExchangeView>(view,"exchangeView");
                    for(int i=0;i<600 && Field<Task<string[]>>(exchange,"listing")!=null;i++)
                    {Frame(controller,input,Vector2.zero);yield return null;}
                    string name=(binaryFormat?"oprint-":"kg-")+(russian?"ru":"en");
                    string destination=Path.Combine(store.ExportDirectory,name);
                    Field<TMP_InputField>(exchange,"fileName").SetTextWithoutNotify(name);
                    for(int i=0;i<5;i++)NativeClick(view,null,"ExchangeFormat");
                    Require(Field<bool>(exchange,"kgFormat") && Field<OpenKeepExport.Consumer?>(exchange,"openKeepTarget")==null &&
                        !Field<bool>(exchange,"archive") && !Field<bool>(exchange,"planBuild"),"KG format flags conflict");
                    NativeClick(view,null,"ExchangeSubmit");yield return null;
                    for(int i=0;i<600 && Field<Task<KgExport>>(exchange,"kgInspection")!=null;i++)
                    {Frame(controller,input,Vector2.zero);yield return null;}
                    var review=Field<KgExport>(exchange,"kgReview");
                    var details=Field<TMP_Text>(exchange,"contentsDetails");
                    string title=Field<RectTransform>(exchange,"contentsPanel").Find("ExchangeContentsTitle").GetComponent<TMP_Text>().text;
                    Require(title==BuildWorksLocalization.Text("exchange.kg_title") && (!binaryFormat || !title.Contains("YAML")),
                        "Binary file review has a YAML title");
                    Require(review!=null && review.Blockers.Count==0 && !Directory.Exists(destination) &&
                        details.text.Contains(name+extension) && details.text.Contains(name+".buildworks.json") &&
                        details.text.Contains(BuildWorksLocalization.Text("exchange.kg_archived",review.ArchivedFields)) &&
                        Field<Button>(exchange,"primary").interactable,"KG review lacks destination/losses or enables writing before confirmation");
                    NativeClick(view,null,"ExchangeContentsBack");yield return null;
                    Require(!Directory.Exists(destination),"KG Back wrote files");
                    NativeClick(view,null,"ExchangeSubmit");yield return null;
                    for(int i=0;i<600 && Field<Task<KgExport>>(exchange,"kgInspection")!=null;i++)
                    {Frame(controller,input,Vector2.zero);yield return null;}
                    NativeClick(view,null,"ExchangeSubmit");yield return null;
                    for(int i=0;i<600 && Field<Task<string>>(exchange,"planExport")!=null;i++)
                    {Frame(controller,input,Vector2.zero);yield return null;}
                    Require(Field<bool>(exchange,"completed") && Directory.GetFiles(destination).Length==2 &&
                        File.Exists(Path.Combine(destination,name+extension)),"KG UI did not export pair: "+Field<TMP_Text>(exchange,"message").text);
                    NativeClick(view,null,"ExchangeClose");yield return null;
                }
                var blocked=binary.ImportCopy();blocked.parts[0].scale.x=2;
                view.ShowExchange((parent,textFont,closed)=>new BlueprintExchangeView(parent,textFont,store,blocked,closed));yield return null;
                var denied=Field<BlueprintExchangeView>(view,"exchangeView");
                for(int i=0;i<600 && Field<Task<string[]>>(denied,"listing")!=null;i++)
                {Frame(controller,input,Vector2.zero);yield return null;}
                for(int i=0;i<5;i++)NativeClick(view,null,"ExchangeFormat");
                NativeClick(view,null,"ExchangeSubmit");yield return null;
                for(int i=0;i<600 && Field<Task<KgExport>>(denied,"kgInspection")!=null;i++)
                {Frame(controller,input,Vector2.zero);yield return null;}
                Require(!Field<Button>(denied,"primary").interactable && Field<Button>(denied,"contentsBack").interactable &&
                    Field<KgExport>(denied,"kgReview").Blockers.Count>0,"KG blocked scale still allows export");
                NativeClick(view,null,"ExchangeClose");yield return null;
                File.WriteAllText(Path.Combine(output,"kg-ui-acceptance.json"),"{\"passed\":true,\"languages\":2,\"formats\":[\"yml\",\"oprint\"],\"binaryActualFixture\":true,\"nativeGame\":false,\"pairedFiles\":true,\"backNoWrite\":true,\"blockedScale\":true}");
            }
        }
        finally{BuildWorksLocalization.RussianLanguage=false;Object.Destroy(prefab);}
    }
}
