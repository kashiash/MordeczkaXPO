using System.Drawing;
using DevExpress.Drawing;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.Editors;
using XafXPODynAssem.Module.BusinessObjects;

namespace XafXPODynAssem.Module.Services;
public sealed class DataDrivenAppearanceAdapter : IAppearanceRuleProperties
{
    public DataDrivenAppearanceAdapter(AppearanceRuleData r, Type t) { AppearanceItemType="ViewItem"; Context=string.IsNullOrWhiteSpace(r.Context)?"Any":r.Context; Criteria=r.Criteria; DeclaringType=t; Method=r.Method??""; TargetItems=string.IsNullOrWhiteSpace(r.TargetItems)?"*":r.TargetItems; BackColor=r.BackColor; FontColor=r.FontColor; FontStyle=r.FontStyle.HasValue?(DXFontStyle)(int)r.FontStyle.Value:null; Visibility=r.Visibility; Priority=r.Priority; }
    public string AppearanceItemType{get;set;} public string Context{get;set;} public string Criteria{get;set;} public Type DeclaringType{get;} public string Method{get;set;} public string TargetItems{get;set;} public Color? BackColor{get;set;} public Color? FontColor{get;set;} public DXFontStyle? FontStyle{get;set;} public ViewItemVisibility? Visibility{get;set;} public bool? Enabled{get;set;} public int Priority{get;set;}
}
