﻿using System;
using Microsoft.AspNetCore.Mvc;

namespace ContosoMaintenance.WebAPI.Controllers
{
    [Route("/api/ping")]
    public class Status : Controller
    {

        [HttpGet]
        public string Ping()
        {
            return "pong";
        }
    }
}
