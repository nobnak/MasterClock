namespace MasterClock
{
    public partial class App : Application
    {
        double width = 400;
        double height = 400;

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());
            window.Width = width;
            window.Height = height;
            return window;
        }
    }
}