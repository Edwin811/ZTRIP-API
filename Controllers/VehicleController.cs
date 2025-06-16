// File: Z_TRIP/Controllers/VehicleController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Z_TRIP.Models;
using System.Collections.Generic;
using System;
using Z_TRIP.Exceptions;

namespace Z_TRIP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehicleController : ControllerBase
    {
        private readonly string _constr;

        public VehicleController(IConfiguration config)
        {
            _constr = config.GetConnectionString("koneksi")!;
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult<List<Vehicle>> GetAll()
        {
            try
            {
                var ctx = new VehicleContext(_constr);
                var list = ctx.GetAllVehicles();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // GET api/vehicle/{id}
        [HttpGet("{id:int}")]
        public ActionResult<Vehicle> GetById(int id)
        {
            try
            {
                var ctx = new VehicleContext(_constr);
                var vehicle = ctx.GetVehicleById(id);

                if (vehicle == null)
                    throw new ResourceNotFoundException($"Kendaraan dengan ID {id} tidak ditemukan.");

                return Ok(vehicle);
            }
            catch (ResourceNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Global exception handler will catch this
                throw;
            }
        }

        // GET api/vehicle/merk/{merk}
        [HttpGet("merk/{merk}")]
        public ActionResult<List<Vehicle>> ByMerk(string merk)
        {
            var ctx = new VehicleContext(_constr);
            var list = ctx.GetVehicleByMerk(merk);
            if (list == null || list.Count == 0)
                return NotFound($"Kendaraan dengan merk '{merk}' tidak ditemukan.");
            return Ok(list);
        }

        // GET api/vehicle/kapasitas/{kapasitas}
        [HttpGet("kapasitas/{kapasitas:int}")]
        public ActionResult<List<Vehicle>> ByKapasitas(int kapasitas)
        {
            var ctx = new VehicleContext(_constr);
            var list = ctx.GetVehicleByKapasitas(kapasitas);
            if (list == null || list.Count == 0)
                return NotFound($"Kendaraan dengan kapasitas '{kapasitas}' tidak ditemukan.");
            return Ok(list);
        }


        // POST api/vehicle
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult Create(Vehicle vehicle)
        {
            // Validasi input
            if (vehicle == null)
                return BadRequest(new { message = "Data tidak valid" });

            var ctx = new VehicleContext(_constr);
            int insertId = ctx.InsertVehicle(vehicle);

            if (insertId > 0)
            {
                var newVehicle = ctx.GetVehicleById(insertId);
                return CreatedAtAction(nameof(GetById), new { id = insertId }, newVehicle);
            }
            else
            {
                return StatusCode(500, new { message = "Gagal menambahkan kendaraan" });
            }
        }

        // PUT api/vehicle/{id}
        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult Update(int id, Vehicle vehicle)
        {
            if (vehicle == null || vehicle.Id != id)
                return BadRequest(new { message = "Data tidak valid" });

            var ctx = new VehicleContext(_constr);
            var existingVehicle = ctx.GetVehicleById(id);

            if (existingVehicle == null)
                return NotFound(new { message = $"Kendaraan dengan ID {id} tidak ditemukan" });

            // Update vehicle
            bool success = ctx.UpdateVehicle(id, vehicle);
            if (success)
            {
                var updatedVehicle = ctx.GetVehicleById(id);
                return Ok(updatedVehicle);
            }
            else
            {
                return StatusCode(500, new { message = "Gagal memperbarui kendaraan" });
            }
        }

        // DELETE api/vehicle/{id}
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult Delete(int id)
        {
            var ctx = new VehicleContext(_constr);
            var existingVehicle = ctx.GetVehicleById(id);

            if (existingVehicle == null)
                return NotFound(new { message = $"Kendaraan dengan ID {id} tidak ditemukan" });

            // Check if there are units linked to this vehicle
            var unitCtx = new VehicleUnitsContext(_constr);
            var units = unitCtx.GetVehicleUnitsByVehicleId(id);

            if (units != null && units.Count > 0)
                return BadRequest(new { message = "Tidak dapat menghapus kendaraan karena masih memiliki unit" });

            // Delete vehicle
            bool success = ctx.DeleteVehicle(id);
            if (success)
            {
                return Ok(new { message = $"Kendaraan dengan ID {id} berhasil dihapus" });
            }
            else
            {
                return StatusCode(500, new { message = "Gagal menghapus kendaraan" });
            }
        }

        // GET api/vehicle/filter?category=mobil&merk=Toyota&kapasitas=5&priceMin=200000&priceMax=500000
        [HttpGet("filter")]
        [AllowAnonymous]
        public ActionResult<List<Vehicle>> Filter(
            [FromQuery] vehicle_category_enum? category,
            [FromQuery] string? merk,
            [FromQuery] int? kapasitas,
            [FromQuery] decimal? priceMin,
            [FromQuery] decimal? priceMax,
            [FromQuery] string? name)
        {
            try
            {
                // Validasi range harga
                if (priceMin.HasValue && priceMax.HasValue && priceMin > priceMax)
                {
                    throw new ValidationException("Harga minimum tidak boleh lebih besar dari harga maksimum");
                }

                var ctx = new VehicleContext(_constr);
                var vehicles = ctx.FilterVehicles(category, merk, kapasitas, priceMin, priceMax, name);

                if (!vehicles.Any())
                    return NotFound(new { message = "Tidak ditemukan kendaraan dengan kriteria tersebut" });

                return Ok(vehicles);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                throw; // Let global handler handle it
            }
        }
    }
}
