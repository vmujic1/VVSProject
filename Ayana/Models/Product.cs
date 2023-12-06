﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography.X509Certificates;

namespace Ayana.Models
{
    public class Product
    {
        [Key]
        public int ProductID { get; set; }
        public string Name { get; set; }

        public string ImageUrl { get; set; }
        public double Price { get; set; }

        [DisplayName("Flower type")]
        public string? FlowerType { get; set; }

        public int Stock { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }

        public string productType { get; set; }

        public Product()
        {

        }
    }
}
