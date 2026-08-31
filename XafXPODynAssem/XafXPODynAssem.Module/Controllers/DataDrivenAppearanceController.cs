using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using XafXPODynAssem.Module.BusinessObjects;

namespace XafXPODynAssem.Module.Controllers;
public sealed class DataDrivenAppearanceController : ViewController<ObjectView>
{
    AppearanceController appearance; List<Services.DataDrivenAppearanceAdapter> adapters;
    protected override void OnActivated(){base.OnActivated(); appearance=Frame.GetController<AppearanceController>(); if(appearance==null)return; adapters=Load(); appearance.ResetRulesCache(); appearance.CollectAppearanceRules+=Collect; appearance.Refresh(); AppearanceRuleCommitController.RulesCommitted+=RefreshRules;}
    protected override void OnDeactivated(){AppearanceRuleCommitController.RulesCommitted-=RefreshRules; if(appearance!=null)appearance.CollectAppearanceRules-=Collect; appearance=null; adapters=null; base.OnDeactivated();}
    void RefreshRules(object s,EventArgs e){if(appearance==null)return; adapters=Load(); appearance.ResetRulesCache(); appearance.Refresh();}
    void Collect(object s,CollectAppearanceRulesEventArgs e){if(adapters!=null)foreach(var x in adapters)e.AppearanceRules.Add(x);}
    List<Services.DataDrivenAppearanceAdapter> Load(){var t=View.ObjectTypeInfo?.Type;if(t==null)return [];try{using var os=Application.CreateObjectSpace(typeof(AppearanceRuleData));var p=t.FullName??t.Name;var viewId=View.Id??"";return os.GetObjects<AppearanceRuleData>(CriteriaOperator.Parse("IsDisabled = false AND DataTypeName = ? AND (ViewId = ? OR ViewId Is Null OR ViewId = '')",p,viewId)).Select(x=>new Services.DataDrivenAppearanceAdapter(x,t)).ToList();}catch{return [];}}
}
public sealed class AppearanceRuleCommitController : ObjectViewController<DetailView, AppearanceRuleData>
{
    public static event EventHandler RulesCommitted;
    protected override void OnActivated(){base.OnActivated();ObjectSpace.Committed+=(_,_)=>RulesCommitted?.Invoke(null,EventArgs.Empty);}
}
