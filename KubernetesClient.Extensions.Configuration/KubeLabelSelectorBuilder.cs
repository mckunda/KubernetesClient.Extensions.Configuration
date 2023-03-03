namespace KubeOps.KubernetesClient.Extensions.Configuration;

public class KubeLabelSelectorBuilder
{
    private readonly List<KubeLabelSubSelectorBuilder> _selectors = new();

    public KubeLabelSubSelectorBuilder Label(string name)
    {
        var selector = new KubeLabelSubSelectorBuilder(this, name);
        _selectors.Add(selector);
        return selector;
    }

    public override string ToString()
    {
        return string.Join(",", _selectors.Select(s => s.ToString()));
    }
}

public class KubeLabelSubSelectorBuilder
{
    private KubeLabelSelectorBuilder SelectorBuilder { get; }
    private readonly string _name;
    private Func<string>? _toStringImpl;

    public KubeLabelSubSelectorBuilder(KubeLabelSelectorBuilder selectorBuilder, string name)
    {
        SelectorBuilder = selectorBuilder;
        _name = name;
    }

    public KubeLabelSelectorBuilder Exists()
    {
        _toStringImpl = () => _name;
        return SelectorBuilder;
    }

    public KubeLabelSelectorBuilder NotExists()
    {
        _toStringImpl = () => $"!{_name}";
        return SelectorBuilder;
    }

    public KubeLabelSelectorBuilder Has(params string[] values)
    {
        _toStringImpl = () => $"{_name} in ({string.Join(", ", values)})";
        return SelectorBuilder;
    }

    public KubeLabelSelectorBuilder HasNot(params string[] values)
    {
        _toStringImpl = () => $"{_name} notin ({string.Join(", ", values)})";
        return SelectorBuilder;
    }

    public override string ToString()
    {
        return _toStringImpl?.Invoke() ?? throw new Exception(); // TODO: concrete exception
    }
}
