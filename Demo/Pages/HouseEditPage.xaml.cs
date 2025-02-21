using Demo.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Demo.Pages
{
    public partial class HouseEditPage : Page
    {
        private House _currentHouse;
        private HouseEditService _service = new HouseEditService();

        public HouseEditPage(int? houseId = null)
        {
            InitializeComponent();
            LoadData(houseId);
        }

        private async void LoadData(int? houseId)
        {
            try
            {
                // Загрузка данных
                ComplexComboBox.ItemsSource = await _service.GetActiveComplexesAsync();
                await LoadHousesList();

                // Инициализация формы
                if (houseId.HasValue)
                    await LoadHouseData(houseId.Value);
                else
                    ResetForm();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка инициализации", ex);
            }
        }

        private async Task LoadHouseData(int houseId)
        {
            try
            {
                _currentHouse = await _service.GetHouseByIdAsync(houseId);
                if (_currentHouse == null) return;

                ComplexComboBox.SelectedValue = _currentHouse.ResidentialComplexID;
                StreetTextBox.Text = _currentHouse.Street;
                NumberTextBox.Text = _currentHouse.Number;
                BuildingCostControl.Value = _currentHouse.BuildingCost;
                ValueAddedControl.Value = _currentHouse.HouseValueAdded;
            }
            catch (Exception ex)
            {
                ShowError("Ошибка загрузки данных дома", ex);
            }
        }

        private async Task LoadHousesList()
        {
            try
            {
                HousesGrid.ItemsSource = await _service.GetAllHousesAsync();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка загрузки списка", ex);
            }
        }

        private bool ValidateForm()
        {
            if (ComplexComboBox.SelectedValue == null)
                return ShowValidationError("Не выбран жилой комплекс!");

            if (string.IsNullOrWhiteSpace(StreetTextBox.Text))
                return ShowValidationError("Укажите улицу!");

            if (string.IsNullOrWhiteSpace(NumberTextBox.Text))
                return ShowValidationError("Укажите номер дома!");

            if (BuildingCostControl.Value == null || BuildingCostControl.Value < 0)
                return ShowValidationError("Некорректные затраты!");

            if (ValueAddedControl.Value == null || ValueAddedControl.Value < 0)
                return ShowValidationError("Некорректный коэффициент!");

            return true;
        }

        private bool ShowValidationError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                var house = _currentHouse ?? new House();
                UpdateHouseFromForm(house);

                await _service.SaveHouseAsync(house);
                await RefreshData();
                ShowSuccess("Данные сохранены успешно!");
            }
            catch (Exception ex)
            {
                ShowError("Ошибка сохранения", ex);
            }
        }

        private void UpdateHouseFromForm(House house)
        {
            house.ResidentialComplexID = (int)ComplexComboBox.SelectedValue;
            house.Street = StreetTextBox.Text;
            house.Number = NumberTextBox.Text;
            house.BuildingCost = BuildingCostControl.Value.GetValueOrDefault();
            house.HouseValueAdded = ValueAddedControl.Value.GetValueOrDefault();
        }

        private async Task RefreshData()
        {
            await LoadHousesList();
            ResetForm();
        }

        private void ResetForm()
        {
            _currentHouse = null;
            ComplexComboBox.SelectedIndex = -1;
            StreetTextBox.Clear();
            NumberTextBox.Clear();
            BuildingCostControl.Value = null;
            ValueAddedControl.Value = null;
        }

        private void NewButton_Click(object sender, RoutedEventArgs e) => ResetForm();

        private void CancelButton_Click(object sender, RoutedEventArgs e) => NavigationService.GoBack();

        private void HousesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HousesGrid.SelectedItem is House selectedHouse)
                NavigationService.Navigate(new HouseEditPage(selectedHouse.ID));
        }

        private void ShowError(string title, Exception ex)
        {
            MessageBox.Show($"{title}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowSuccess(string message)
        {
            MessageBox.Show(message, "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    internal class HouseEditService
    {
        private const string ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Demo;Trusted_Connection=True;";

        public async Task<List<ResidentialComplex>> GetActiveComplexesAsync()
        {
            var complexes = new List<ResidentialComplex>();

            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand(
                    "SELECT ID, Name FROM ResidentialComplex WHERE IsDeleted = 0",
                    conn);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        complexes.Add(new ResidentialComplex
                        {
                            ID = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }
            return complexes;
        }

        public async Task<House> GetHouseByIdAsync(int id)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand(
                    "SELECT * FROM House WHERE ID = @Id",
                    conn);
                cmd.Parameters.AddWithValue("@Id", id);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    return await reader.ReadAsync() ? MapHouse(reader) : null;
                }
            }
        }

        public async Task SaveHouseAsync(House house)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand(GetSaveQuery(house), conn);
                AddParameters(cmd, house);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<House>> GetAllHousesAsync()
        {
            var houses = new List<House>();

            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand(
                    @"SELECT 
                h.ID,
                h.ResidentialComplexID,
                h.Street,
                h.Number,
                h.BuildingCost,
                h.HouseValueAdded,
                rc.Name 
              FROM House h
              INNER JOIN ResidentialComplex rc ON h.ResidentialComplexID = rc.ID",
                    conn);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        houses.Add(new House
                        {
                            ID = reader.GetInt32(0),
                            ResidentialComplexID = reader.GetInt32(1),
                            Street = reader.GetString(2),
                            Number = reader.GetString(3),
                            BuildingCost = reader.GetInt32(4),
                            HouseValueAdded = reader.GetInt32(5),
                            ResidentialComplex = new ResidentialComplex
                            {
                                Name = reader.GetString(6) // Теперь индекс 6 соответствует rc.Name
                            }
                        });
                    }
                }
            }
            return houses;
        }

        private House MapHouse(SqlDataReader reader)
        {
            return new House
            {
                ID = reader.GetInt32(0),
                ResidentialComplexID = reader.GetInt32(1),
                Street = reader.GetString(2),
                Number = reader.GetString(3),
                BuildingCost = reader.GetInt32(4),
                HouseValueAdded = reader.GetInt32(5)
            };
        }

        private House MapHouseWithComplex(SqlDataReader reader)
        {
            var house = MapHouse(reader);
            house.ResidentialComplex = new ResidentialComplex { Name = reader.GetString(6) };
            return house;
        }

        private string GetSaveQuery(House house)
        {
            return house.ID == 0 ?
                @"INSERT INTO House (
                    ResidentialComplexID,
                    Street,
                    Number,
                    BuildingCost,
                    HouseValueAdded
                  ) VALUES (
                    @ComplexId,
                    @Street,
                    @Number,
                    @BuildingCost,
                    @ValueAdded
                  )" :
                @"UPDATE House SET
                    ResidentialComplexID = @ComplexId,
                    Street = @Street,
                    Number = @Number,
                    BuildingCost = @BuildingCost,
                    HouseValueAdded = @ValueAdded
                  WHERE ID = @Id";
        }

        private void AddParameters(SqlCommand cmd, House house)
        {
            cmd.Parameters.AddWithValue("@ComplexId", house.ResidentialComplexID);
            cmd.Parameters.AddWithValue("@Street", house.Street);
            cmd.Parameters.AddWithValue("@Number", house.Number);
            cmd.Parameters.AddWithValue("@BuildingCost", house.BuildingCost);
            cmd.Parameters.AddWithValue("@ValueAdded", house.HouseValueAdded);

            if (house.ID != 0)
                cmd.Parameters.AddWithValue("@Id", house.ID);
        }
    }
}