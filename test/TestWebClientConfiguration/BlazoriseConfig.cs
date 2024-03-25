using Blazorise;
using Blazorise.Bootstrap.Modules;
using Blazorise.Licensing;
using Blazorise.Localization;
using Blazorise.Modules;
using Blazorise.Utilities;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace TestWebClientConfiguration;

public static class BlazoriseConfig
{
    public static void AddBootstrapProviders( TestServiceProvider services )
    {
        //services.AddSingleton<IComponentDisposer, ComponentDisposer>();
        services.AddSingleton<IIdGenerator>( new IdGenerator() );
        services.AddSingleton<IEditContextValidator>( sp => new EditContextValidator( new ValidationMessageLocalizerAttributeFinder(), sp ) );
        services.AddSingleton( new Mock<IIconProvider>().Object );
        services.AddSingleton<IValidationHandlerFactory, ValidationHandlerFactory>();
        services.AddSingleton<ValidatorValidationHandler>();
        services.AddSingleton<PatternValidationHandler>();
        services.AddSingleton<DataAnnotationValidationHandler>();
        services.AddSingleton<IVersionProvider, VersionProvider>();
        services.AddScoped<ITextLocalizerService, TextLocalizerService>();
        services.AddScoped( typeof( ITextLocalizer<> ), typeof( TextLocalizer<> ) );

        // Shared component context. Must be defined as scoped as we want to make it available for the user session.
        services.AddScoped<IModalSharedContext, ModalSharedContext>();

        void ConfigureOptions(BlazoriseOptions options)
        {
            options.Immediate = true;
        }

        services.AddSingleton( ConfigureOptions );
        services.AddSingleton<BlazoriseOptions>();


        services.AddScoped<IJSUtilitiesModule, JSUtilitiesModule>();
        services.AddScoped<IJSButtonModule, JSButtonModule>();
        services.AddScoped<IJSClosableModule, JSClosableModule>();
        services.AddScoped<IJSBreakpointModule, JSBreakpointModule>();
        services.AddScoped<IJSTextEditModule, JSTextEditModule>();
        services.AddScoped<IJSMemoEditModule, JSMemoEditModule>();
        services.AddScoped<IJSNumericPickerModule, JSNumericPickerModule>();
        services.AddScoped<IJSDatePickerModule, JSDatePickerModule>();
        services.AddScoped<IJSTimePickerModule, JSTimePickerModule>();
        services.AddScoped<IJSColorPickerModule, JSColorPickerModule>();
        services.AddScoped<IJSFileEditModule, JSFileEditModule>();
        services.AddScoped<IJSTableModule, JSTableModule>();
        services.AddScoped<IJSInputMaskModule, JSInputMaskModule>();
        services.AddScoped<IJSDropdownModule, JSDropdownModule>();
        services.AddScoped<IJSDragDropModule, JSDragDropModule>();

        //services.AddScoped<IJSModalModule, Bootstrap.Modules.BootstrapJSModalModule>();
        //services.AddScoped<IJSTooltipModule, Bootstrap.Modules.BootstrapJSTooltipModule>();

        services.AddScoped<BlazoriseLicenseProvider>();
        services.AddScoped<BlazoriseLicenseChecker>();

        services.AddMemoryCache();
    }

    internal class VersionProvider : IVersionProvider
    {
        public string Version => "";

        public string MilestoneVersion => "";
    }

    public static class JSInterop
    {
        public static void AddButton( BunitJSInterop jsInterop )
        {
            AddUtilities( jsInterop );

            var module = jsInterop.SetupModule( new JSButtonModule( jsInterop.JSRuntime, new VersionProvider() ).ModuleFileName );
            module.SetupVoid( "import", _ => true ).SetVoidResult();
            module.SetupVoid( "initialize", _ => true ).SetVoidResult();
            module.SetupVoid( "destroy", _ => true ).SetVoidResult();
        }

        public static void AddBreakpoint( BunitJSInterop jsInterop )
        {
            AddUtilities( jsInterop );

            var module = jsInterop.SetupModule( new JSBreakpointModule( jsInterop.JSRuntime, new VersionProvider() ).ModuleFileName );
            module.SetupVoid( "import", _ => true ).SetVoidResult();
            module.SetupVoid( "initialize", _ => true ).SetVoidResult();
            module.SetupVoid( "destroy", _ => true ).SetVoidResult();
            module.SetupVoid( "registerBreakpointComponent", _ => true ).SetVoidResult();
            module.SetupVoid( "unregisterBreakpointComponent", _ => true ).SetVoidResult();
            module.SetupVoid( "getBreakpoint", _ => true ).SetVoidResult();
        }

        public static void AddTextEdit( BunitJSInterop jsInterop )
        {
            AddUtilities( jsInterop );

            var module = jsInterop.SetupModule( new JSTextEditModule( jsInterop.JSRuntime, new VersionProvider() ).ModuleFileName );
            module.SetupVoid( "import", _ => true ).SetVoidResult();
            module.SetupVoid( "initialize", _ => true ).SetVoidResult();
            module.SetupVoid( "destroy", _ => true ).SetVoidResult();
        }

        public static void AddDatePicker( BunitJSInterop jsInterop )
        {
            AddUtilities( jsInterop );

            jsInterop.SetupModule( new JSDatePickerModule( jsInterop.JSRuntime, new VersionProvider() ).ModuleFileName )
                .SetupVoid( "initialize", _ => true ).SetVoidResult();
        }

        public static void AddClosable( BunitJSInterop jsInterop )
        {
            AddUtilities( jsInterop );

            var module = jsInterop.SetupModule( new JSClosableModule( jsInterop.JSRuntime, new VersionProvider() ).ModuleFileName );
            module.SetupVoid( "import", _ => true ).SetVoidResult();
            module.SetupVoid( "registerClosableComponent", _ => true ).SetVoidResult();
            module.SetupVoid( "unregisterClosableComponent", _ => true ).SetVoidResult();
            module.SetupVoid( "registerClosableLightComponent", _ => true ).SetVoidResult();
            module.SetupVoid( "unregisterClosableLightComponent", _ => true ).SetVoidResult();
        }

        public static void AddNumericEdit( BunitJSInterop jsInterop )
        {
            AddUtilities( jsInterop );

            var module = jsInterop.SetupModule( new JSNumericPickerModule( jsInterop.JSRuntime, new VersionProvider() ).ModuleFileName );
            module.SetupVoid( "import", _ => true ).SetVoidResult();
            module.SetupVoid( "initialize", _ => true ).SetVoidResult();
            module.SetupVoid( "destroy", _ => true ).SetVoidResult();
        }

        public static void AddUtilities( BunitJSInterop jsInterop )
        {
            var module = jsInterop.SetupModule( new JSUtilitiesModule( jsInterop.JSRuntime, new VersionProvider() ).ModuleFileName );
            module.SetupVoid( "import", _ => true ).SetVoidResult();
            module.SetupVoid( "setProperty", _ => true ).SetVoidResult();
            module.Setup<string>( "getUserAgent", _ => true ).SetResult( String.Empty );
            module.SetupVoid( "scrollElementIntoView", _ => true ).SetVoidResult();
            module.SetupVoid( "focus", _ => true ).SetVoidResult();
            module.SetupVoid( "log", _ => true ).SetVoidResult();
        }

        public static void AddModal( BunitJSInterop jsInterop )
        {
            AddUtilities( jsInterop );

            var module = jsInterop.SetupModule( new MockJsModule( jsInterop.JSRuntime, new VersionProvider() ).ModuleFileName );
            module.SetupVoid( "import", _ => true ).SetVoidResult();
            module.SetupVoid( "open", _ => true ).SetVoidResult();
            module.SetupVoid( "close", _ => true ).SetVoidResult();
        }


        public static void AddTable( BunitJSInterop jsInterop )
        {
            AddUtilities( jsInterop );

            var module = jsInterop.SetupModule( new JSTableModule( jsInterop.JSRuntime, new VersionProvider() ).ModuleFileName );
            module.SetupVoid( "initializeTableFixedHeader", _ => true ).SetVoidResult();
            module.SetupVoid( "destroyTableFixedHeader", _ => true ).SetVoidResult();
            module.SetupVoid( "fixedHeaderScrollTableToPixels", _ => true ).SetVoidResult();
            module.SetupVoid( "fixedHeaderScrollTableToRow", _ => true ).SetVoidResult();
            module.SetupVoid( "initializeResizable", _ => true ).SetVoidResult();
            module.SetupVoid( "destroyResizable", _ => true ).SetVoidResult();
        }

        public static void AddDropdown( BunitJSInterop jsInterop )
        {
            AddUtilities( jsInterop );

            var module = jsInterop.SetupModule( new JSDropdownModule( jsInterop.JSRuntime, new VersionProvider() ).ModuleFileName );
            module.SetupVoid( "initialize", _ => true );
            module.SetupVoid( "destroy", _ => true );
            module.SetupVoid( "show", _ => true );
            module.SetupVoid( "hide", _ => true );
        }

        public static void AddDragDrop( BunitJSInterop jsInterop )
        {
            AddUtilities( jsInterop );

            var module = jsInterop.SetupModule( new JSDragDropModule( jsInterop.JSRuntime, new VersionProvider() ).ModuleFileName );
            module.SetupVoid( "initialize", _ => true ).SetVoidResult();
            module.SetupVoid( "initializeThrottledDragEvents", _ => true ).SetVoidResult();
            module.SetupVoid( "destroy", _ => true ).SetVoidResult();
            module.SetupVoid( "destroyThrottledDragEvents", _ => true ).SetVoidResult();
        }
        
        public static void AddTooltip(BunitJSInterop jsInterop)
        {
            var module = jsInterop.SetupModule(new BootstrapJSTooltipModule(jsInterop.JSRuntime, new VersionProvider()).ModuleFileName);

            module.SetupVoid( "initialize", _ => true ).SetVoidResult();
            module.SetupVoid( "destroy", _ => true ).SetVoidResult();
            module.SetupVoid( "updateContent", _ => true ).SetVoidResult();
        }
    }

    private class MockJsModule : JSModalModule
    {
        public MockJsModule( IJSRuntime jsRuntime, IVersionProvider versionProvider ) : base( jsRuntime, versionProvider )
        {
        }

        public override string ModuleFileName => $"./_content/Blazorise.Bootstrap/modal.js?v={VersionProvider.Version}";
    }
}