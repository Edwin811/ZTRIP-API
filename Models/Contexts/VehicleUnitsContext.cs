// File: Models/VehicleUnitsContext.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Z_TRIP.Helpers;

namespace Z_TRIP.Models.Contexts
{
    public class VehicleUnitsContext
    {
        private readonly string _constr;

        public VehicleUnitsContext(string constr)
        {
            _constr = constr;
        }

        // Ambil semua vehicle units
        public List<VehicleUnit> GetAllVehicleUnits()
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                SELECT vu.id, vu.code, vu.vehicle_id, vu.price_per_day, vu.vehicle_image, vu.description, 
                       vu.created_at, vu.updated_at
                FROM vehicle_units vu
                ORDER BY vu.created_at DESC";

            using var cmd = db.GetNpgsqlCommand(query);
            using var reader = cmd.ExecuteReader();

            var vehicleUnits = new List<VehicleUnit>();
            while (reader.Read())
            {
                vehicleUnits.Add(new VehicleUnit
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    VehicleId = reader.GetInt32(2),
                    PricePerDay = reader.GetDecimal(3),
                    VehicleImage = reader.IsDBNull(4) ? null : (byte[])reader["vehicle_image"],
                    Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                    CreatedAt = reader.GetDateTime(6),
                    UpdatedAt = reader.GetDateTime(7)
                });
            }

            return vehicleUnits;
        }

        // Ambil vehicle unit berdasarkan ID
        public VehicleUnit? GetVehicleUnitById(int id)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                SELECT vu.id, vu.code, vu.vehicle_id, vu.price_per_day, vu.vehicle_image, vu.description, 
                       vu.created_at, vu.updated_at
                FROM vehicle_units vu
                WHERE vu.id = @Id
                LIMIT 1";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new VehicleUnit
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    VehicleId = reader.GetInt32(2),
                    PricePerDay = reader.GetDecimal(3),
                    VehicleImage = reader.IsDBNull(4) ? null : (byte[])reader["vehicle_image"],
                    Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                    CreatedAt = reader.GetDateTime(6),
                    UpdatedAt = reader.GetDateTime(7)
                };
            }

            return null;
        }

        // Ambil vehicle unit berdasarkan Code
        public VehicleUnit? GetVehicleUnitByCode(string code)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                SELECT vu.id, vu.code, vu.vehicle_id, vu.price_per_day, vu.vehicle_image, 
                       vu.description, vu.created_at, vu.updated_at
                FROM vehicle_units vu
                WHERE LOWER(vu.code) = LOWER(@Code)
                LIMIT 1";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Code", code.Trim());

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new VehicleUnit
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    VehicleId = reader.GetInt32(2),
                    PricePerDay = reader.GetDecimal(3),
                    VehicleImage = reader.IsDBNull(4) ? null : (byte[])reader["vehicle_image"],
                    Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                    CreatedAt = reader.GetDateTime(6),
                    UpdatedAt = reader.GetDateTime(7)
                };
            }

            return null;
        }

        // Ambil vehicle units berdasarkan VehicleId
        public List<VehicleUnit> GetVehicleUnitsByVehicleId(int vehicleId)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                SELECT vu.id, vu.code, vu.vehicle_id, vu.price_per_day, vu.vehicle_image, vu.description, 
                       vu.created_at, vu.updated_at
                FROM vehicle_units vu
                WHERE vu.vehicle_id = @VehicleId
                ORDER BY vu.created_at DESC";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@VehicleId", vehicleId);
            using var reader = cmd.ExecuteReader();

            var vehicleUnits = new List<VehicleUnit>();
            while (reader.Read())
            {
                vehicleUnits.Add(new VehicleUnit
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    VehicleId = reader.GetInt32(2),
                    PricePerDay = reader.GetDecimal(3),
                    VehicleImage = reader.IsDBNull(4) ? null : (byte[])reader["vehicle_image"],
                    Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                    CreatedAt = reader.GetDateTime(6),
                    UpdatedAt = reader.GetDateTime(7)
                });
            }

            return vehicleUnits;
        }

        // Tambah vehicle unit baru
        public int AddVehicleUnit(VehicleUnit unit)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                INSERT INTO vehicle_units (
                    code, 
                    vehicle_id, 
                    price_per_day, 
                    description, 
                    vehicle_image,
                    created_at, 
                    updated_at
                )
                VALUES (
                    @Code, 
                    @VehicleId, 
                    @PricePerDay, 
                    @Description,
                    @VehicleImage,
                    CURRENT_TIMESTAMP, 
                    CURRENT_TIMESTAMP
                )
                RETURNING id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Code", unit.Code);
            cmd.Parameters.AddWithValue("@VehicleId", unit.VehicleId);
            cmd.Parameters.AddWithValue("@PricePerDay", unit.PricePerDay);
            cmd.Parameters.AddWithValue("@Description", unit.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@VehicleImage", unit.VehicleImage ?? (object)DBNull.Value);

            try
            {
                var result = cmd.ExecuteScalar();
                return result == null ? 0 : Convert.ToInt32(result);
            }
            finally
            {
                db.CloseConnection();
            }
        }

        // Update vehicle unit
        public bool UpdateVehicleUnit(VehicleUnit unit)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE vehicle_units
                SET code = @Code,
                    vehicle_id = @VehicleId,
                    price_per_day = @PricePerDay,
                    description = @Description,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @Id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Code", unit.Code);
            cmd.Parameters.AddWithValue("@VehicleId", unit.VehicleId);
            cmd.Parameters.AddWithValue("@PricePerDay", unit.PricePerDay);
            cmd.Parameters.AddWithValue("@Description", unit.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Id", unit.Id);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Delete vehicle unit
        public bool DeleteVehicleUnit(int id)
        {
            var db = new SqlDBHelper(_constr);
            const string query = "DELETE FROM vehicle_units WHERE id = @Id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Id", id);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Update gambar kendaraan
        public bool UpdateVehicleImage(int id, byte[]? imageData)
        {
            var db = new SqlDBHelper(_constr);
            string query;

            if (imageData == null)
            {
                // Set vehicle_image menjadi NULL jika imageData null
                query = @"
                    UPDATE vehicle_units
                    SET vehicle_image = NULL,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE id = @Id";

                using var cmd = db.GetNpgsqlCommand(query);
                cmd.Parameters.AddWithValue("@Id", id);

                return cmd.ExecuteNonQuery() > 0;
            }
            else
            {
                // Update dengan data gambar baru
                query = @"
                    UPDATE vehicle_units
                    SET vehicle_image = @VehicleImage,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE id = @Id";

                using var cmd = db.GetNpgsqlCommand(query);
                cmd.Parameters.AddWithValue("@VehicleImage", imageData);
                cmd.Parameters.AddWithValue("@Id", id);

                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // Tambahkan method untuk validasi
        public bool IsCodeUnique(string code, int? excludeId = null)
        {
            var db = new SqlDBHelper(_constr);
            string query = "SELECT COUNT(*) FROM vehicle_units WHERE LOWER(code) = LOWER(@Code)";

            if (excludeId.HasValue)
            {
                query += " AND id != @ExcludeId";
            }

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Code", code.Trim());
            if (excludeId.HasValue)
            {
                cmd.Parameters.AddWithValue("@ExcludeId", excludeId.Value);
            }

            int count = Convert.ToInt32(cmd.ExecuteScalar());
            return count == 0;
        }
    }
}
