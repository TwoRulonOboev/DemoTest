using Demo.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Demo.Pages
{
    public partial class ResidentialComplexesPage : Page
    {
        private ResidentialComplexService _service = new ResidentialComplexService();
        private ResidentialComplex _currentComplex;

        public ResidentialComplexesPage()
        {
            InitializeComponent();
            LoadData();
        }

        private async void LoadData()
        {
            try
            {
                ComplexesGrid.ItemsSource = await _service.GetAllComplexesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            try
            {
                var complex = _currentComplex ?? new ResidentialComplex();
                UpdateComplexData(complex);

                if (complex.Status == "План" && await _service.HasSoldApartments(complex.ID))
                {
                    MessageBox.Show("Невозможно установить статус 'План' - есть проданные квартиры!");
                    return;
                }

                await _service.SaveComplexAsync(complex);
                LoadData();
                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void UpdateComplexData(ResidentialComplex complex)
        {
            complex.Name = NameTextBox.Text;
            complex.Status = (StatusComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            complex.ComplexValueAdded = ValueAddedControl.Value ?? 0;
            complex.BuildingCost = BuildingCostControl.Value ?? 0;
            complex.City = CityTextBox.Text;
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Введите название комплекса!");
                return false;
            }

            if (StatusComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите статус!");
                return false;
            }

            return true;
        }

        private async void ComplexesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComplexesGrid.SelectedItem is ResidentialComplex selectedComplex)
            {
                _currentComplex = selectedComplex;
                NameTextBox.Text = selectedComplex.Name;
                CityTextBox.Text = selectedComplex.City;
                StatusComboBox.SelectedValue = selectedComplex.Status;
                ValueAddedControl.Value = selectedComplex.ComplexValueAdded;
                BuildingCostControl.Value = selectedComplex.BuildingCost;

                // Загрузка домов
                HousesGrid.ItemsSource = await _service.GetHousesByComplexAsync(selectedComplex.ID);
            }
        }

        private void NewButton_Click(object sender, RoutedEventArgs e) => ResetForm();

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentComplex != null && MessageBox.Show("Удалить комплекс?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _service.DeleteComplexAsync(_currentComplex.ID);
                LoadData();
                ResetForm();
            }
        }

        private void ResetForm()
        {
            _currentComplex = null;
            NameTextBox.Clear();
            CityTextBox.Clear();
            StatusComboBox.SelectedIndex = -1;
            ValueAddedControl.Value = null;
            BuildingCostControl.Value = null;
            HousesGrid.ItemsSource = null;
        }
    }

    public class ResidentialComplexService
    {
        private const string ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Demo;Trusted_Connection=True;";

        public async Task<List<ResidentialComplex>> GetAllComplexesAsync()
        {
            var complexes = new List<ResidentialComplex>();

            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand(
                    "SELECT ID, Name, City, Status, BuildingCost, ComplexValueAdded FROM ResidentialComplex WHERE IsDeleted = 0",
                    conn);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        complexes.Add(new ResidentialComplex
                        {
                            ID = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            City = reader.GetString(2),
                            Status = reader.GetString(3),
                            BuildingCost = reader.GetInt32(4),
                            ComplexValueAdded = reader.GetInt32(5)
                        });
                    }
                }
            }
            return complexes;
        }

        public async Task SaveComplexAsync(ResidentialComplex complex)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                var query = complex.ID == 0 ? GetInsertQuery() : GetUpdateQuery();
                var cmd = new SqlCommand(query, conn);

                cmd.Parameters.AddWithValue("@Name", complex.Name);
                cmd.Parameters.AddWithValue("@City", complex.City);
                cmd.Parameters.AddWithValue("@Status", complex.Status);
                cmd.Parameters.AddWithValue("@BuildingCost", complex.BuildingCost);
                cmd.Parameters.AddWithValue("@ValueAdded", complex.ComplexValueAdded);

                if (complex.ID != 0)
                    cmd.Parameters.AddWithValue("@Id", complex.ID);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task DeleteComplexAsync(int complexId)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand("UPDATE ResidentialComplex SET IsDeleted = 1 WHERE ID = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", complexId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<bool> HasSoldApartments(int complexId)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand(
                    @"SELECT COUNT(*) 
                      FROM Apartment a
                      INNER JOIN House h ON a.HouseID = h.ID
                      WHERE h.ResidentialComplexID = @ComplexId AND a.IsSold = 1",
                    conn);

                cmd.Parameters.AddWithValue("@ComplexId", complexId);
                return (int)await cmd.ExecuteScalarAsync() > 0;
            }
        }

        public async Task<List<House>> GetHousesByComplexAsync(int complexId)
        {
            var houses = new List<House>();

            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand(
                    "SELECT ID, Street, Number FROM House WHERE ResidentialComplexID = @ComplexId AND IsDeleted = 0",
                    conn);

                cmd.Parameters.AddWithValue("@ComplexId", complexId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        houses.Add(new House
                        {
                            ID = reader.GetInt32(0),
                            Street = reader.GetString(1),
                            Number = reader.GetString(2)
                        });
                    }
                }
            }
            return houses;
        }

        private string GetInsertQuery() => @"
            INSERT INTO ResidentialComplex (
                Name, City, Status, BuildingCost, ComplexValueAdded
            ) VALUES (
                @Name, @City, @Status, @BuildingCost, @ValueAdded
            )";

        private string GetUpdateQuery() => @"
            UPDATE ResidentialComplex SET
                Name = @Name,
                City = @City,
                Status = @Status,
                BuildingCost = @BuildingCost,
                ComplexValueAdded = @ValueAdded
            WHERE ID = @Id";
    }
}