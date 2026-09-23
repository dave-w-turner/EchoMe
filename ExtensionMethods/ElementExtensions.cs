namespace EchoMe.ExtensionMethods;

public static class ElementExtensions
{
    public static T? FindAncestorByName<T>(this Element element, string name) where T : Element
    {
        Element current = element.Parent;

        while (current != null)
        {
            if (current is T targetElement &&
                (targetElement.StyleId == name || targetElement.AutomationId == name))
            {
                return targetElement;
            }

            current = current.Parent;
        }

        return null;
    }

    public static T? FindAncestor<T>(this Element? element) where T : Element
    {
        if (element == null) return null;

        Element? current = element.Parent;

        while (current != null)
        {
            if (current is T ancestor)
            {
                return ancestor;
            }

            current = current.Parent;
        }

        return null;
    }
}