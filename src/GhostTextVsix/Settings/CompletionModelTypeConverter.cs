using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using GhostTextVsix.Completion.Providers;

namespace GhostTextVsix.Settings;

internal sealed class CompletionModelTypeConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
    {
        return true;
    }

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
    {
        return false;
    }

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
    {
        var providerType = GetOptions(context)?.AutoCompletionProvider ?? CompletionProviderType.NotConfigured;
        return new StandardValuesCollection(ProviderRegistry.GetModelCandidates(providerType).ToArray());
    }

    private static DeepSeekOptionsPage GetOptions(ITypeDescriptorContext context)
    {
        if (context?.Instance is DeepSeekOptionsPage options)
        {
            return options;
        }

        if (context?.Instance is IEnumerable<object> instances)
        {
            return instances.OfType<DeepSeekOptionsPage>().FirstOrDefault();
        }

        return null;
    }
}
