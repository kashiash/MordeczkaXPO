using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using XafXPODynAssem.Module.BusinessObjects;

namespace XafXPODynAssem.Module.Controllers;
public sealed class AppearanceRuleTargetItemsController : ObjectViewController<DetailView, AppearanceRuleData>
{
    protected override void OnActivated(){base.OnActivated();View.CurrentObjectChanged+=Changed;ObjectSpace.ObjectChanged+=ObjectChanged;Update();}
    protected override void OnDeactivated(){View.CurrentObjectChanged-=Changed;ObjectSpace.ObjectChanged-=ObjectChanged;base.OnDeactivated();}
    void Changed(object s,EventArgs e)=>Update();
    void ObjectChanged(object s,ObjectChangedEventArgs e){if(e.PropertyName==nameof(AppearanceRuleData.DataTypeName)||e.PropertyName==nameof(AppearanceRuleData.DataType))Update();}
    void Update(){var rule=ViewCurrentObject;if(rule==null)return;if(View.FindItem(nameof(AppearanceRuleData.TargetItems)) is not PropertyEditor editor||editor.Model is not IModelCommonMemberViewItem model)return;var info=rule.DataType==null?null:XafTypesInfo.Instance.FindTypeInfo(rule.DataType);var names=new List<string>{"*"};if(info!=null)names.AddRange(info.Members.Where(m=>m.IsVisible&&!m.IsService).Select(m=>m.Name).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase));model.PredefinedValues=string.Join(";",names);}
}
