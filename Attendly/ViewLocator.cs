    using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Attendly.ViewModels;

namespace Attendly;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);
        if (type is null)
        {
            // ViewModels can live in platform-specific assemblies (e.g. Attendly.Desktop's
            // admin-only screens) that Type.GetType() alone won't search.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(name);
                if (type is not null) break;
            }
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}