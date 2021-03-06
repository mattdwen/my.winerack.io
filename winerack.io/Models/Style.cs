﻿using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace winerack.Models
{
    public class Style
    {
        #region Properties

        public int ID { get; set; }

        [Required]
        public string Name { get; set; }

        #endregion Properties

        #region Relationships

        [JsonIgnore]
        public virtual ICollection<Wine> Wines { get; set; }

        #endregion Relationships

        #region Public Methods

        public static IEnumerable<Style> GetStyles()
        {
            var context = new ApplicationDbContext();
            return context.Styles
                .OrderBy(r => r.Name);
        }

        #endregion Public Methods
    }
}