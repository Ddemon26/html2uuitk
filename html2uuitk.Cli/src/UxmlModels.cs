using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace html2uuitk.Cli;

internal static class UxmlNamespaces
{
    public const string Ui = "UnityEngine.UIElements";
    public const string UiEditor = "UnityEditor.UIElements";
    public const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";
}

[XmlInclude(typeof(VisualElementTag))]
[XmlInclude(typeof(ScrollViewTag))]
[XmlInclude(typeof(ListViewTag))]
[XmlInclude(typeof(TreeViewTag))]
[XmlInclude(typeof(MultiColumnListViewTag))]
[XmlInclude(typeof(MultiColumnTreeViewTag))]
[XmlInclude(typeof(GroupBoxTag))]
[XmlInclude(typeof(LabelTag))]
[XmlInclude(typeof(ButtonTag))]
[XmlInclude(typeof(ToggleTag))]
[XmlInclude(typeof(ToggleButtonGroupTag))]
[XmlInclude(typeof(ScrollerTag))]
[XmlInclude(typeof(TextFieldTag))]
[XmlInclude(typeof(FoldoutTag))]
[XmlInclude(typeof(SliderTag))]
[XmlInclude(typeof(SliderIntTag))]
[XmlInclude(typeof(MinMaxSliderTag))]
[XmlInclude(typeof(ProgressBarTag))]
[XmlInclude(typeof(DropdownFieldTag))]
[XmlInclude(typeof(EnumFieldTag))]
[XmlInclude(typeof(RadioButtonTag))]
[XmlInclude(typeof(RadioButtonGroupTag))]
[XmlInclude(typeof(TabTag))]
[XmlInclude(typeof(TabViewTag))]
[XmlInclude(typeof(IntegerFieldTag))]
[XmlInclude(typeof(FloatFieldTag))]
[XmlInclude(typeof(LongFieldTag))]
[XmlInclude(typeof(DoubleFieldTag))]
[XmlInclude(typeof(Hash128FieldTag))]
[XmlInclude(typeof(Vector2FieldTag))]
[XmlInclude(typeof(Vector3FieldTag))]
[XmlInclude(typeof(Vector4FieldTag))]
[XmlInclude(typeof(RectFieldTag))]
[XmlInclude(typeof(BoundsFieldTag))]
[XmlInclude(typeof(UnsignedIntegerFieldTag))]
[XmlInclude(typeof(UnsignedLongFieldTag))]
[XmlInclude(typeof(Vector2IntFieldTag))]
[XmlInclude(typeof(Vector3IntFieldTag))]
[XmlInclude(typeof(RectIntFieldTag))]
[XmlInclude(typeof(BoundsIntFieldTag))]
public abstract class UxmlElement
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlAttribute("class")]
    public string? Class { get; set; }

    [XmlAttribute("view-data-key")]
    public string? ViewDataKey { get; set; }

    [XmlAttribute("binding-path")]
    public string? BindingPath { get; set; }

    [XmlAttribute("tooltip")]
    public string? Tooltip { get; set; }

    [XmlAttribute("style")]
    public string? InlineStyle { get; set; }

    [XmlAttribute("picking-mode")]
    public string? PickingMode { get; set; }

    [XmlAttribute("focusable")]
    public string? Focusable { get; set; }

    [XmlAttribute("tabindex")]
    public string? TabIndex { get; set; }

    [XmlAnyAttribute]
    public XmlAttribute[]? AdditionalAttributes { get; set; }

    [XmlElement("VisualElement", typeof(VisualElementTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("ScrollView", typeof(ScrollViewTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("ListView", typeof(ListViewTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("TreeView", typeof(TreeViewTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("MultiColumnListView", typeof(MultiColumnListViewTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("MultiColumnTreeView", typeof(MultiColumnTreeViewTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("GroupBox", typeof(GroupBoxTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Label", typeof(LabelTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Button", typeof(ButtonTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Toggle", typeof(ToggleTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("ToggleButtonGroup", typeof(ToggleButtonGroupTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Scroller", typeof(ScrollerTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("TextField", typeof(TextFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Foldout", typeof(FoldoutTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Slider", typeof(SliderTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("SliderInt", typeof(SliderIntTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("MinMaxSlider", typeof(MinMaxSliderTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("ProgressBar", typeof(ProgressBarTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("DropdownField", typeof(DropdownFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("EnumField", typeof(EnumFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("RadioButton", typeof(RadioButtonTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("RadioButtonGroup", typeof(RadioButtonGroupTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Tab", typeof(TabTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("TabView", typeof(TabViewTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("IntegerField", typeof(IntegerFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("FloatField", typeof(FloatFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("LongField", typeof(LongFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("DoubleField", typeof(DoubleFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Hash128Field", typeof(Hash128FieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Vector2Field", typeof(Vector2FieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Vector3Field", typeof(Vector3FieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Vector4Field", typeof(Vector4FieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("RectField", typeof(RectFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("BoundsField", typeof(BoundsFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("UnsignedIntegerField", typeof(UnsignedIntegerFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("UnsignedLongField", typeof(UnsignedLongFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Vector2IntField", typeof(Vector2IntFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("Vector3IntField", typeof(Vector3IntFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("RectIntField", typeof(RectIntFieldTag), Namespace = UxmlNamespaces.Ui)]
    [XmlElement("BoundsIntField", typeof(BoundsIntFieldTag), Namespace = UxmlNamespaces.Ui)]
    public List<UxmlElement> Children { get; set; } = new();
}

public abstract class TextElement : UxmlElement
{
    [XmlAttribute("text")]
    public string? Text { get; set; }
}

public abstract class LabeledElement : UxmlElement
{
    [XmlAttribute("label")]
    public string? Label { get; set; }
}

public abstract class LabeledValueElement : LabeledElement
{
    [XmlAttribute("value")]
    public string? Value { get; set; }
}

public abstract class ValueElement : UxmlElement
{
    [XmlAttribute("value")]
    public string? Value { get; set; }
}

[XmlRoot("UXML", Namespace = UxmlNamespaces.Ui)]
[XmlType("UXML", Namespace = UxmlNamespaces.Ui)]
public sealed class UxmlDocument : UxmlElement
{
    private XmlSerializerNamespaces? _namespaces;

    [XmlNamespaceDeclarations]
    public XmlSerializerNamespaces Namespaces
    {
        get
        {
            if (_namespaces is null)
            {
                _namespaces = new XmlSerializerNamespaces();
                _namespaces.Add("ui", UxmlNamespaces.Ui);
                _namespaces.Add("uie", UxmlNamespaces.UiEditor);
                _namespaces.Add("xsi", UxmlNamespaces.Xsi);
            }

            return _namespaces;
        }
        set => _namespaces = value;
    }

    [XmlAttribute("noNamespaceSchemaLocation", Namespace = UxmlNamespaces.Xsi)]
    public string? SchemaLocation { get; set; }

    [XmlAttribute("editor-extension-mode")]
    public bool EditorExtensionMode { get; set; }
}

[XmlType("VisualElement", Namespace = UxmlNamespaces.Ui)]
public sealed class VisualElementTag : UxmlElement
{
}

[XmlType("ScrollView", Namespace = UxmlNamespaces.Ui)]
public sealed class ScrollViewTag : UxmlElement
{
    [XmlAttribute("horizontal-scroller-visibility")]
    public string? HorizontalScrollerVisibility { get; set; }

    [XmlAttribute("vertical-scroller-visibility")]
    public string? VerticalScrollerVisibility { get; set; }

    [XmlAttribute("show-horizontal-scroller")]
    public string? ShowHorizontalScroller { get; set; }

    [XmlAttribute("show-vertical-scroller")]
    public string? ShowVerticalScroller { get; set; }

    [XmlAttribute("scroll-deceleration-rate")]
    public string? ScrollDecelerationRate { get; set; }

    [XmlAttribute("elasticity")]
    public string? Elasticity { get; set; }
}

[XmlType("ListView", Namespace = UxmlNamespaces.Ui)]
public sealed class ListViewTag : UxmlElement
{
    [XmlAttribute("items-source")]
    public string? ItemsSource { get; set; }

    [XmlAttribute("selection-type")]
    public string? SelectionType { get; set; }

    [XmlAttribute("show-bound-collection-size")]
    public string? ShowBoundCollectionSize { get; set; }

    [XmlAttribute("virtualization-method")]
    public string? VirtualizationMethod { get; set; }
}

[XmlType("TreeView", Namespace = UxmlNamespaces.Ui)]
public sealed class TreeViewTag : UxmlElement
{
    [XmlAttribute("items-source")]
    public string? ItemsSource { get; set; }

    [XmlAttribute("selection-type")]
    public string? SelectionType { get; set; }
}

[XmlType("MultiColumnListView", Namespace = UxmlNamespaces.Ui)]
public sealed class MultiColumnListViewTag : UxmlElement
{
    [XmlAttribute("items-source")]
    public string? ItemsSource { get; set; }

    [XmlAttribute("selection-type")]
    public string? SelectionType { get; set; }
}

[XmlType("MultiColumnTreeView", Namespace = UxmlNamespaces.Ui)]
public sealed class MultiColumnTreeViewTag : UxmlElement
{
    [XmlAttribute("items-source")]
    public string? ItemsSource { get; set; }

    [XmlAttribute("selection-type")]
    public string? SelectionType { get; set; }
}

[XmlType("GroupBox", Namespace = UxmlNamespaces.Ui)]
public sealed class GroupBoxTag : UxmlElement
{
    [XmlAttribute("header")]
    public string? Header { get; set; }
}

[XmlType("Label", Namespace = UxmlNamespaces.Ui)]
public sealed class LabelTag : TextElement
{
}

[XmlType("Button", Namespace = UxmlNamespaces.Ui)]
public sealed class ButtonTag : TextElement
{
}

[XmlType("Toggle", Namespace = UxmlNamespaces.Ui)]
public sealed class ToggleTag : LabeledValueElement
{
}

[XmlType("ToggleButtonGroup", Namespace = UxmlNamespaces.Ui)]
public sealed class ToggleButtonGroupTag : LabeledValueElement
{
}

[XmlType("Scroller", Namespace = UxmlNamespaces.Ui)]
public sealed class ScrollerTag : ValueElement
{
    [XmlAttribute("high-value")]
    public string? HighValue { get; set; }

    [XmlAttribute("low-value")]
    public string? LowValue { get; set; }

    [XmlAttribute("direction")]
    public string? Direction { get; set; }
}

[XmlType("TextField", Namespace = UxmlNamespaces.Ui)]
public sealed class TextFieldTag : LabeledElement
{
    [XmlAttribute("placeholder-text")]
    public string? PlaceholderText { get; set; }

    [XmlAttribute("multiline")]
    public string? Multiline { get; set; }

    [XmlAttribute("is-password-field")]
    public string? IsPasswordField { get; set; }
}

[XmlType("Foldout", Namespace = UxmlNamespaces.Ui)]
public sealed class FoldoutTag : TextElement
{
    [XmlAttribute("value")]
    public string? Value { get; set; }
}

[XmlType("Slider", Namespace = UxmlNamespaces.Ui)]
public sealed class SliderTag : LabeledValueElement
{
    [XmlAttribute("low-value")]
    public string? LowValue { get; set; }

    [XmlAttribute("high-value")]
    public string? HighValue { get; set; }

    [XmlAttribute("page-size")]
    public string? PageSize { get; set; }

    [XmlAttribute("direction")]
    public string? Direction { get; set; }
}

[XmlType("SliderInt", Namespace = UxmlNamespaces.Ui)]
public sealed class SliderIntTag : LabeledValueElement
{
    [XmlAttribute("low-value")]
    public string? LowValue { get; set; }

    [XmlAttribute("high-value")]
    public string? HighValue { get; set; }
}

[XmlType("MinMaxSlider", Namespace = UxmlNamespaces.Ui)]
public sealed class MinMaxSliderTag : LabeledElement
{
    [XmlAttribute("value")]
    public string? Value { get; set; }

    [XmlAttribute("low-limit")]
    public string? LowLimit { get; set; }

    [XmlAttribute("high-limit")]
    public string? HighLimit { get; set; }
}

[XmlType("ProgressBar", Namespace = UxmlNamespaces.Ui)]
public sealed class ProgressBarTag : ValueElement
{
    [XmlAttribute("title")]
    public string? Title { get; set; }

    [XmlAttribute("low-value")]
    public string? LowValue { get; set; }

    [XmlAttribute("high-value")]
    public string? HighValue { get; set; }
}

[XmlType("DropdownField", Namespace = UxmlNamespaces.Ui)]
public sealed class DropdownFieldTag : LabeledValueElement
{
    [XmlAttribute("choices")]
    public string? Choices { get; set; }

    [XmlAttribute("index")]
    public string? Index { get; set; }
}

[XmlType("EnumField", Namespace = UxmlNamespaces.Ui)]
public sealed class EnumFieldTag : LabeledValueElement
{
    [XmlAttribute("type")]
    public string? Type { get; set; }
}

[XmlType("RadioButton", Namespace = UxmlNamespaces.Ui)]
public sealed class RadioButtonTag : LabeledValueElement
{
}

[XmlType("RadioButtonGroup", Namespace = UxmlNamespaces.Ui)]
public sealed class RadioButtonGroupTag : LabeledValueElement
{
}

[XmlType("Tab", Namespace = UxmlNamespaces.Ui)]
public sealed class TabTag : LabeledElement
{
    [XmlAttribute("text")]
    public string? Text { get; set; }
}

[XmlType("TabView", Namespace = UxmlNamespaces.Ui)]
public sealed class TabViewTag : UxmlElement
{
    [XmlAttribute("value")]
    public string? Value { get; set; }
}

[XmlType("IntegerField", Namespace = UxmlNamespaces.Ui)]
public sealed class IntegerFieldTag : LabeledValueElement
{
}

[XmlType("FloatField", Namespace = UxmlNamespaces.Ui)]
public sealed class FloatFieldTag : LabeledValueElement
{
}

[XmlType("LongField", Namespace = UxmlNamespaces.Ui)]
public sealed class LongFieldTag : LabeledValueElement
{
}

[XmlType("DoubleField", Namespace = UxmlNamespaces.Ui)]
public sealed class DoubleFieldTag : LabeledValueElement
{
}

[XmlType("Hash128Field", Namespace = UxmlNamespaces.Ui)]
public sealed class Hash128FieldTag : LabeledValueElement
{
}

[XmlType("Vector2Field", Namespace = UxmlNamespaces.Ui)]
public sealed class Vector2FieldTag : LabeledValueElement
{
}

[XmlType("Vector3Field", Namespace = UxmlNamespaces.Ui)]
public sealed class Vector3FieldTag : LabeledValueElement
{
}

[XmlType("Vector4Field", Namespace = UxmlNamespaces.Ui)]
public sealed class Vector4FieldTag : LabeledValueElement
{
}

[XmlType("RectField", Namespace = UxmlNamespaces.Ui)]
public sealed class RectFieldTag : LabeledValueElement
{
}

[XmlType("BoundsField", Namespace = UxmlNamespaces.Ui)]
public sealed class BoundsFieldTag : LabeledValueElement
{
}

[XmlType("UnsignedIntegerField", Namespace = UxmlNamespaces.Ui)]
public sealed class UnsignedIntegerFieldTag : LabeledValueElement
{
}

[XmlType("UnsignedLongField", Namespace = UxmlNamespaces.Ui)]
public sealed class UnsignedLongFieldTag : LabeledValueElement
{
}

[XmlType("Vector2IntField", Namespace = UxmlNamespaces.Ui)]
public sealed class Vector2IntFieldTag : LabeledValueElement
{
}

[XmlType("Vector3IntField", Namespace = UxmlNamespaces.Ui)]
public sealed class Vector3IntFieldTag : LabeledValueElement
{
}

[XmlType("RectIntField", Namespace = UxmlNamespaces.Ui)]
public sealed class RectIntFieldTag : LabeledValueElement
{
}

[XmlType("BoundsIntField", Namespace = UxmlNamespaces.Ui)]
public sealed class BoundsIntFieldTag : LabeledValueElement
{
}
