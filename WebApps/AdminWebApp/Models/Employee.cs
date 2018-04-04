﻿using System;

using Newtonsoft.Json;

namespace ContosoMaintenance.AdminWebApp.Models
{
    public class Employee : BaseUser
    {
        [JsonProperty("startDate")]
        public DateTime StartDate { get; set; }
    }
}
