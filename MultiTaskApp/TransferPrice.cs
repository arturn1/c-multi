using System;
using System.Collections.Generic;

namespace MultiTaskApp.Models
{
    public class Entity
    {
        public Guid Id { get; set; }
        public int? Cik { get; set; } // Agora pode ser nulo
        public string? EntityName { get; set; } // Agora pode ser nulo
        public ICollection<Fact>? Facts { get; set; } // Agora pode ser nulo
    }

    public class Fact
    {
        public Guid Id { get; set; }
        public Guid? EntityId { get; set; } // Agora pode ser nulo
        public string? Namespace { get; set; } // Agora pode ser nulo
        public string? Name { get; set; } // Agora pode ser nulo
        public string? Label { get; set; } // Agora pode ser nulo
        public string? Description { get; set; } // Agora pode ser nulo
        public ICollection<Unit>? Units { get; set; } // Agora pode ser nulo

        public Entity? Entity { get; set; } // Agora pode ser nulo
    }

    public class Unit
    {
        public Guid Id { get; set; }
        public Guid? FactId { get; set; } // Agora pode ser nulo
        public string? UnitType { get; set; } // Agora pode ser nulo
        public ICollection<Value>? Values { get; set; } // Agora pode ser nulo

        public Fact? Fact { get; set; } // Agora pode ser nulo
    }

    public class Value
    {
        public Guid Id { get; set; }
        public Guid? UnitId { get; set; } // Agora pode ser nulo
        public DateTime? StartDate { get; set; } // Agora pode ser nulo
        public DateTime? EndDate { get; set; } // Agora pode ser nulo
        public decimal? Val { get; set; } // Agora pode ser nulo
        public string? Accn { get; set; } // Agora pode ser nulo
        public int? FiscalYear { get; set; } // Agora pode ser nulo
        public string? FiscalPeriod { get; set; } // Agora pode ser nulo
        public string? Form { get; set; } // Agora pode ser nulo
        public DateTime? FiledDate { get; set; } // Agora pode ser nulo
        public string? Frame { get; set; } // Agora pode ser nulo

        public Unit? Unit { get; set; } // Agora pode ser nulo
    }
}