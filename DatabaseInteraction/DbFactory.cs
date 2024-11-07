namespace DatabaseInteraction;

public static class DbFactory
{
    public static ApplicationContext GetContext()
    {
        return new ApplicationContext();
    }

    public static void DisposeContext(ApplicationContext context)
    {
        context.Dispose();
    }
}