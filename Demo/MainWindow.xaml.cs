using System.Windows;
using System.Windows.Navigation;
using Demo.Pages;

namespace Demo
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnApartments_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new HouseEditPage());
        }

        private void btnHouses_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new HousesPage());
        }

        private void btnResidentialComplexes_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ResidentialComplexesPage());
        }
    }
}
