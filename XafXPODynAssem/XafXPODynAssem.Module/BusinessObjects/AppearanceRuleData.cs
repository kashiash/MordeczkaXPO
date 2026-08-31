using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Utils;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace XafXPODynAssem.Module.BusinessObjects;

public enum AppearanceFontStyle { Regular = 0, Bold = 1, Italic = 2, Underline = 4, Strikeout = 8 }
public enum AppearanceContext { Any, DetailView, ListView }

[DefaultClassOptions]
[NavigationItem("Zarządzanie schematem")]
[DefaultProperty(nameof(Name))]
public class AppearanceRuleData : BaseObject
{
    public AppearanceRuleData(Session session) : base(session) { }
    string name, dataTypeName, context = "Any", criteria = "", targetItems = "*", method = "";
    string viewId = "";
    bool isDisabled; int priority; int? backColorValue; int? fontColorValue;
    [Required, Size(256)] public string Name { get => name; set => SetPropertyValue(nameof(Name), ref name, value); }
    public bool IsDisabled { get => isDisabled; set => SetPropertyValue(nameof(IsDisabled), ref isDisabled, value); }
    [Size(256), XafDisplayName("View ID (puste = wszystkie widoki)")]
    public string ViewId { get => viewId; set => SetPropertyValue(nameof(ViewId), ref viewId, value); }
    [Browsable(false), Required, Size(1024)] public string DataTypeName { get => dataTypeName; set => SetPropertyValue(nameof(DataTypeName), ref dataTypeName, value); }
    [NonPersistent, TypeConverter(typeof(LocalizedClassInfoTypeConverter)), ImmediatePostData]
    [XafDisplayName("Typ docelowy")]
    public Type DataType { get => string.IsNullOrWhiteSpace(DataTypeName) ? null : Type.GetType(DataTypeName, false) ?? XafTypesInfo.Instance.FindTypeInfo(DataTypeName)?.Type; set { DataTypeName = value?.FullName ?? ""; Criteria = ""; TargetItems = "*"; } }
    [Browsable(false), Size(128)] public string Context { get => context; set => SetPropertyValue(nameof(Context), ref context, value); }
    [NonPersistent, ImmediatePostData, XafDisplayName("Kontekst")] public AppearanceContext ContextValue { get => Enum.TryParse(Context, out AppearanceContext x) ? x : AppearanceContext.Any; set => Context = value.ToString(); }
    [CriteriaOptions(nameof(DataType)), Size(SizeAttribute.Unlimited)] public string Criteria { get => criteria; set => SetPropertyValue(nameof(Criteria), ref criteria, value); }
    [Size(2048)] public string TargetItems { get => targetItems; set => SetPropertyValue(nameof(TargetItems), ref targetItems, value); }
    public int Priority { get => priority; set => SetPropertyValue(nameof(Priority), ref priority, value); }
    [Browsable(false), Size(256)] public string Method { get => method; set => SetPropertyValue(nameof(Method), ref method, value); }
    public ViewItemVisibility? Visibility { get; set; }
    [Browsable(false)] public int? BackColorValue { get => backColorValue; set => SetPropertyValue(nameof(BackColorValue), ref backColorValue, value); }
    [NonPersistent] public Color? BackColor { get => backColorValue.HasValue ? Color.FromArgb(backColorValue.Value) : null; set => BackColorValue = value?.ToArgb(); }
    [Browsable(false)] public int? FontColorValue { get => fontColorValue; set => SetPropertyValue(nameof(FontColorValue), ref fontColorValue, value); }
    [NonPersistent] public Color? FontColor { get => fontColorValue.HasValue ? Color.FromArgb(fontColorValue.Value) : null; set => FontColorValue = value?.ToArgb(); }
    public AppearanceFontStyle? FontStyle { get; set; }
    [Browsable(false)]
    [RuleFromBoolProperty("AppearanceRule_ValidCriteria", DefaultContexts.Save, "Criteria is not valid.", UsedProperties = nameof(Criteria))]
    public bool IsCriteriaValid { get { try { if (!string.IsNullOrWhiteSpace(Criteria)) DevExpress.Data.Filtering.CriteriaOperator.Parse(Criteria); return true; } catch { return false; } } }
}
