using Demo.Models;
using System;
using System.Collections.Generic;

using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Demo.Pages
{
    public partial class HousesPage : Page
    {
        private HouseService _houseService;
        private List<ResidentialComplex> _complexes;
        private House _selectedHouse;

        public HousesPage()
        {
            InitializeComponent();
            _houseService = new HouseService();
            LoadData();
        }

        private async void LoadData()
        {
            try
            {
                _complexes = await _houseService.GetResidentialComplexesAsync();
                ComplexFilterComboBox.ItemsSource = _complexes;
                ComplexComboBox.ItemsSource = _complexes;
                await LoadHousesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private async Task LoadHousesAsync()
        {
            try
            {
                var filterComplexId = (int?)ComplexFilterComboBox.SelectedValue;
                var filterAddress = AddressFilterTextBox.Text;

                var houses = await _houseService.GetHousesAsync(filterComplexId, filterAddress);
                HousesGrid.ItemsSource = houses;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки домов: {ex.Message}");
            }
        }

        private async void SaveHouse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ComplexComboBox.SelectedValue == null)
                {
                    MessageBox.Show("Выберите жилой комплекс!");
                    return;
                }

                var house = _selectedHouse ?? new House();
                house.ResidentialComplexID = (int)ComplexComboBox.SelectedValue;
                house.Street = StreetTextBox.Text;
                house.Number = NumberTextBox.Text;

                // Парсинг числовых значений с проверкой
                if (!int.TryParse(BuildingCostTextBox.Text, out int buildingCost))
                {
                    MessageBox.Show("Некорректная стоимость строительства!");
                    return;
                }
                house.BuildingCost = buildingCost;

                if (!int.TryParse(ValueAddedTextBox.Text, out int valueAdded))
                {
                    MessageBox.Show("Некорректная добавочная стоимость!");
                    return;
                }
                house.HouseValueAdded = valueAdded;

                await _houseService.SaveHouseAsync(house);
                await LoadHousesAsync();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void ClearForm()
        {
            _selectedHouse = null;
            ComplexComboBox.SelectedIndex = -1;
            StreetTextBox.Clear();
            NumberTextBox.Clear();
            BuildingCostTextBox.Clear();
            ValueAddedTextBox.Clear();
        }

        private async void DeleteHouse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MessageBox.Show("Удалить дом?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    var house = (House)((Button)sender).DataContext;
                    await _houseService.DeleteHouseAsync(house.ID);
                    await LoadHousesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}");
            }
        }

        private void HousesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _selectedHouse = (House)HousesGrid.SelectedItem;
                if (_selectedHouse != null)
                {
                    ComplexComboBox.SelectedValue = _selectedHouse.ResidentialComplexID;
                    StreetTextBox.Text = _selectedHouse.Street;
                    NumberTextBox.Text = _selectedHouse.Number;
                    BuildingCostTextBox.Text = _selectedHouse.BuildingCost.ToString();
                    ValueAddedTextBox.Text = _selectedHouse.HouseValueAdded.ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка выбора: {ex.Message}");
            }
        }

        private async void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            ComplexFilterComboBox.SelectedIndex = -1;
            AddressFilterTextBox.Clear();
            await LoadHousesAsync();
        }

        private void NewHouse_Click(object sender, RoutedEventArgs e) => ClearForm();

        private async void Filter_Click(object sender, RoutedEventArgs e)
        {
            await LoadHousesAsync();
        }
    }

    public class HouseService
    {
        private readonly string _connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Demo;Trusted_Connection=True;";

        public async Task<List<House>> GetHousesAsync(int? complexId = null, string addressFilter = null)
        {
            var houses = new List<House>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = @"
                    SELECT 
                        h.ID, 
                        h.ResidentialComplexID, 
                        h.Street, 
                        h.Number,
                        h.BuildingCost,
                        h.HouseValueAdded,
                        rc.Status AS ComplexStatus,
                        (SELECT COUNT(*) FROM Apartment a WHERE a.HouseID = h.ID AND a.IsSold = 1) AS SoldCount,
                        (SELECT COUNT(*) FROM Apartment a WHERE a.HouseID = h.ID AND a.IsSold = 0) AS AvailableCount
                    FROM House h
                    INNER JOIN ResidentialComplex rc ON h.ResidentialComplexID = rc.ID
                    WHERE h.IsDeleted = 0
                        AND (@ComplexId IS NULL OR h.ResidentialComplexID = @ComplexId)
                        AND (@AddressFilter IS NULL OR 
                            (h.Street + ' ' + h.Number) LIKE '%' + @AddressFilter + '%')";

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ComplexId", complexId ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@AddressFilter", addressFilter ?? (object)DBNull.Value);

                using (var reader = await command.ExecuteReaderAsync())
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
                            ResidentialComplex = new ResidentialComplex { Status = reader.GetString(6) },
                            SoldCount = reader.GetInt32(7),
                            AvailableCount = reader.GetInt32(8)
                        });
                    }
                }
            }
            return houses;
        }

        public async Task<List<ResidentialComplex>> GetResidentialComplexesAsync()
        {
            var complexes = new List<ResidentialComplex>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT ID, Name FROM ResidentialComplex WHERE IsDeleted = 0";
                var command = new SqlCommand(query, connection);

                using (var reader = await command.ExecuteReaderAsync())
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

        public async Task SaveHouseAsync(House house)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = house.ID == 0 ?
                    @"INSERT INTO House 
                        (ResidentialComplexID, Street, Number, BuildingCost, HouseValueAdded) 
                      VALUES 
                        (@ComplexId, @Street, @Number, @BuildingCost, @HouseValueAdded)" :
                    @"UPDATE House SET 
                        ResidentialComplexID = @ComplexId, 
                        Street = @Street, 
                        Number = @Number,
                        BuildingCost = @BuildingCost,
                        HouseValueAdded = @HouseValueAdded 
                      WHERE ID = @Id";

                var command = new SqlCommand(query, connection);
                command.Parameters.AddRange(new[]
                {
                    new SqlParameter("@ComplexId", house.ResidentialComplexID),
                    new SqlParameter("@Street", house.Street),
                    new SqlParameter("@Number", house.Number),
                    new SqlParameter("@BuildingCost", house.BuildingCost),
                    new SqlParameter("@HouseValueAdded", house.HouseValueAdded)
                });

                if (house.ID != 0)
                    command.Parameters.Add(new SqlParameter("@Id", house.ID));

                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task DeleteHouseAsync(int houseId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand("UPDATE House SET IsDeleted = 1 WHERE ID = @Id", connection);
                command.Parameters.AddWithValue("@Id", houseId);
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}