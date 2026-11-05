using System;
using System.Collections.Generic;
using System.Text;

namespace PresentationChecks
{
    public static class BlueprintSignPreview { public static bool Allowed=true; }
    public static class CensorShittyWords { public static int Input;public static void Filter(string text,out string result){Input=text.Length;result=text;} }
    public sealed class Piece
    {
        public object m_icon=new object();public string m_description="visible";
        public static implicit operator bool(Piece piece)=>piece!=null;
    }
    public sealed class Blueprint { public string id; }
    public sealed class Renderer
    {
        private bool allowed=true;
        public bool RefreshTextPermission(){bool changed=allowed!=BlueprintSignPreview.Allowed;allowed=BlueprintSignPreview.Allowed;return changed;}
    }
    public static class HammerCatalogView
    {
        public static int Invalidations,Updates;
        public static void Invalidate(){Invalidations++;}
        public static void UpdateThumbnail(string id,object sprite){if(sprite!=null)throw new Exception("Old image survived");Updates++;}
    }
    public sealed class Registry
    {
        public Renderer thumbnailRenderer=new Renderer();
        private bool presentationAllowed=true;
        public bool Allowed=>presentationAllowed;
        public int registeredRevision=17;public string registeredSignature="warm";
        public Dictionary<Piece,Blueprint> blueprintsByPiece=new Dictionary<Piece,Blueprint>();
        private string Description(Blueprint blueprint)=>BlueprintSignPreview.Allowed?"visible":"hidden";
        public void Refresh()=>RefreshPresentationPermission();
        // REGISTRY_METHOD
    }
    public static class Presentation
    {
        // LITERAL_METHOD
    }
    public static class Cases
    {
        private static void Require(bool value,string label){if(!value)throw new InvalidOperationException(label);}
        public static void Run()
        {
            var registry=new Registry();var first=new Piece();var second=new Piece();
            registry.blueprintsByPiece.Add(first,new Blueprint{id="first"});registry.blueprintsByPiece.Add(second,new Blueprint{id="second"});
            BlueprintSignPreview.Allowed=false;
            registry.thumbnailRenderer.RefreshTextPermission(); // Another entry point consumed the renderer change.
            registry.Refresh();
            Require(first.m_description=="hidden" && second.m_description=="hidden" && first.m_icon==null && second.m_icon==null &&
                HammerCatalogView.Invalidations==1 && HammerCatalogView.Updates==2 && registry.registeredRevision==-1 && registry.registeredSignature=="",
                "Consumed renderer permission change left other registered metadata/images visible");
            registry.Refresh();Require(HammerCatalogView.Invalidations==1,"Stable permission rebuilt every frame");
            BlueprintSignPreview.Allowed=true;registry.thumbnailRenderer.RefreshTextPermission();registry.Refresh();
            Require(first.m_description=="visible" && second.m_description=="visible" && HammerCatalogView.Invalidations==2,"Grant did not refresh registry metadata");
            Require(Presentation.Literal(new string('\r',5000)+"marker",128,1)=="" && CensorShittyWords.Input==0,"Skipped characters bypassed metadata input budget");
            Require(Presentation.Literal(new string('a',127)+"😀",128,1)==new string('a',127),"Surrogate split at display boundary");
            Require(Presentation.Literal("<b>literal</b>\\n",128,1)=="＜b＞literal＜/b＞＼n","TMP markup/escape remained active");
            Console.WriteLine("PASS actual registry permission/literal methods: consumed revoke+grant, all registered entries, stable no-rebuild, skipped-input budget, surrogate and markup/escape guards. Host objects/filter substituted; no native game proof.");
        }
    }
}
