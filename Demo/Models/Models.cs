using System.Collections.Generic;

namespace Demo.Models
{
    public class Apartment
    {
        public int ID { get; set; }
        public int HouseID { get; set; }
        public string Number { get; set; }
        public decimal Area { get; set; }
        public int CountOfRooms { get; set; }
        public string Section { get; set; }
        public int Floor { get; set; }
        public bool IsSold { get; set; }
        public decimal BuildingCost { get; set; }
        public decimal ApartmentValueAdded { get; set; }
        public bool IsDeleted { get; set; }

        // Навигационное свойство
        public House House { get; set; }
    }

    public class House
    {
        public int ID { get; set; }
        public int ResidentialComplexID { get; set; }
        public string Street { get; set; }
        public string Number { get; set; }
        public int BuildingCost { get; set; }
        public int HouseValueAdded { get; set; }
        public bool IsDeleted { get; set; }

        // Навигационные свойства
        public ResidentialComplex ResidentialComplex { get; set; }

        // Для отображения
        public int SoldCount { get; set; }
        public int AvailableCount { get; set; }
    }

    public class ResidentialComplex
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public string Status { get; set; }
        public int BuildingCost { get; set; }
        public int ComplexValueAdded { get; set; } // Коэффициент добавочной стоимости

        // Навигационные свойства
        public ICollection<House> Houses { get; set; }  // Один жилой комплекс может содержать несколько домов
    }
}
