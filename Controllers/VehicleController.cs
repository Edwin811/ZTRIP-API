// File: Z_TRIP/Controllers/VehicleController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Z_TRIP.Models;
using System.Collections.Generic;
using System;
using Z_TRIP.Exceptions;
using System.Linq;
using Z_TRIP.Models.Contexts;

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
                // Add validation for id parameter
                if (id <= 0)
                    return BadRequest(new { message = "ID kendaraan harus lebih dari 0" });

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
            try
            {
                // Add validation for merk parameter
                if (string.IsNullOrWhiteSpace(merk))
                    return BadRequest(new { message = "Parameter merk tidak boleh kosong" });

                // Add validation for merk length
                if (merk.Length > 50)
                    return BadRequest(new { message = "Parameter merk terlalu panjang" });

                var ctx = new VehicleContext(_constr);
                var list = ctx.GetVehicleByMerk(merk);

                if (list == null || list.Count == 0)
                    return NotFound($"Kendaraan dengan merk '{merk}' tidak ditemukan.");

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // GET api/vehicle/kapasitas/{kapasitas:int}
        [HttpGet("kapasitas/{kapasitas:int}")]
        public ActionResult<List<Vehicle>> ByKapasitas(int kapasitas)
        {
            try
            {
                // Add validation for kapasitas parameter
                if (kapasitas <= 0)
                    return BadRequest(new { message = "Kapasitas harus lebih dari 0" });

                if (kapasitas > 100) // Set a reasonable upper limit
                    return BadRequest(new { message = "Kapasitas terlalu besar" });

                var ctx = new VehicleContext(_constr);
                var list = ctx.GetVehicleByKapasitas(kapasitas);

                if (list == null || list.Count == 0)
                    return NotFound($"Kendaraan dengan kapasitas '{kapasitas}' tidak ditemukan.");

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }


        // POST api/vehicle
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult Create(Vehicle vehicle)
        {
            try
            {
                // Basic validation
                if (vehicle == null)
                    return BadRequest(new { message = "Data tidak valid" });

                if (string.IsNullOrWhiteSpace(vehicle.Name))
                    return BadRequest(new { message = "Nama kendaraan tidak boleh kosong" });

                if (string.IsNullOrWhiteSpace(vehicle.Merk))
                    return BadRequest(new { message = "Merk kendaraan tidak boleh kosong" });

                if (vehicle.Capacity <= 0)
                    return BadRequest(new { message = "Kapasitas harus lebih dari 0" });

                // Try to create the vehicle
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
            catch (Exception ex)
            {
                // Handle specific errors that might be related to the missing property
                if (ex.Message.Contains("PricePerDay"))
                {
                    return BadRequest(new { message = "Terjadi kesalahan pada harga kendaraan. Mohon periksa data input." });
                }

                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // PUT api/vehicle/{id}
        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult Update(int id, Vehicle vehicle)
        {
            try
            {
                // Add validation for id parameter
                if (id <= 0)
                    return BadRequest(new { message = "ID kendaraan tidak valid" });

                if (vehicle == null)
                    return BadRequest(new { message = "Data tidak valid" });

                if (vehicle.Id != id)
                    return BadRequest(new { message = "ID pada path dan body tidak sesuai" });

                // Add validation for vehicle name
                if (string.IsNullOrWhiteSpace(vehicle.Name))
                    return BadRequest(new { message = "Nama kendaraan tidak boleh kosong" });

                // Add validation for vehicle merk
                if (string.IsNullOrWhiteSpace(vehicle.Merk))
                    return BadRequest(new { message = "Merk kendaraan tidak boleh kosong" });

                // Remove PricePerDay validation

                // Add validation for vehicle capacity
                if (vehicle.Capacity <= 0)
                    return BadRequest(new { message = "Kapasitas harus lebih dari 0" });

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
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // DELETE api/vehicle/{id}
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult Delete(int id)
        {
            try
            {
                // Add validation for id parameter
                if (id <= 0)
                    return BadRequest(new { message = "ID kendaraan tidak valid" });

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
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
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
                // Note: Vehicle model doesn't have PricePerDay property
                // We'll handle price validation in the context method instead

                // Add validation for capacity
                if (kapasitas.HasValue && kapasitas <= 0)
                {
                    throw new ValidationException("Kapasitas harus lebih dari 0");
                }

                // Add validation for merk length if provided
                if (!string.IsNullOrEmpty(merk) && merk.Length > 50)
                {
                    throw new ValidationException("Parameter merk terlalu panjang");
                }

                // Add validation for name length if provided
                if (!string.IsNullOrEmpty(name) && name.Length > 100)
                {
                    throw new ValidationException("Parameter nama terlalu panjang");
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
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }

        // GET api/vehicle/categories
        [HttpGet("categories")]
        [AllowAnonymous]
        public ActionResult GetCategories()
        {
            try
            {
                var categories = Enum.GetValues(typeof(vehicle_category_enum))
                    .Cast<vehicle_category_enum>()
                    .Select(c => new
                    {
                        id = (int)c,
                        name = c.ToString()
                    })
                    .ToList();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error: {ex.Message}" });
            }
        }
    }
}
