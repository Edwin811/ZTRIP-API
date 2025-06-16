using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Z_TRIP.Helpers;
using Npgsql;

namespace Z_TRIP.Models
{
    public class VehicleContext
    {
        private readonly string _constr;

        public VehicleContext(string connectionString)
        {
            _constr = connectionString;
        }

        public List<Vehicle> GetAllVehicles()
        {
            try
            {
                var list = new List<Vehicle>();
                const string sql = @"
                    SELECT id, merk, category, name, description,
                        capacity, created_at, updated_at
                    FROM vehicles;
                ";
                var db = new SqlDBHelper(_constr);
                using var cmd = db.GetNpgsqlCommand(sql);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new Vehicle
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        Merk = reader.GetString(reader.GetOrdinal("merk")),
                        Category = Enum.Parse<vehicle_category_enum>(reader.GetString(reader.GetOrdinal("category"))),
                        Name = reader.GetString(reader.GetOrdinal("name")),
                        Description = reader.IsDBNull(reader.GetOrdinal("description"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("description")),
                        Capacity = reader.GetInt32(reader.GetOrdinal("capacity")),
                        // Hapus PricePerDay dan VehicleImage
                        CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at"))
                                    ? null
                                    : reader.GetDateTime(reader.GetOrdinal("created_at")),
                        UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at"))
                                    ? null
                                    : reader.GetDateTime(reader.GetOrdinal("updated_at")),
                    });
                }

                db.CloseConnection();
                return list;
            }
            catch (Exception ex)
            {
                throw new Exception("Gagal ambil data kendaraan: " + ex.Message, ex);
            }
        }

        public Vehicle? GetVehicleById(int id)
        {
            const string sql = @"
            SELECT id, merk, category, name, description,
                   capacity, created_at, updated_at
              FROM vehicles
             WHERE id = @Id;
            ";
            var db = new SqlDBHelper(_constr);
            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@Id", id);
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                var v = new Vehicle
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Merk = reader.GetString(reader.GetOrdinal("merk")),
                    Category = Enum.Parse<vehicle_category_enum>(reader.GetString(reader.GetOrdinal("category"))),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Description = reader.IsDBNull(reader.GetOrdinal("description"))
                                 ? null
                                 : reader.GetString(reader.GetOrdinal("description")),
                    Capacity = reader.GetInt32(reader.GetOrdinal("capacity")),
                    // Hapus PricePerDay dan VehicleImage
                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at"))
                                 ? null
                                 : reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at"))
                                 ? null
                                 : reader.GetDateTime(reader.GetOrdinal("updated_at")),
                };
                db.CloseConnection();
                return v;
            }

            db.CloseConnection();
            return null;
        }

        public List<Vehicle> GetVehicleByMerk(string merk) =>
            GetAllVehicles()
                .Where(v => v.Merk.Equals(merk, StringComparison.OrdinalIgnoreCase))
                .ToList();

        public List<Vehicle> GetVehicleByKapasitas(int kapasitas) =>
            GetAllVehicles()
                .Where(v => v.Capacity == kapasitas)
                .ToList();

        // Filter harga sekarang harus melalui vehicle_units
        public List<Vehicle> GetVehicleByHarga(int max, int min)
        {
            const string sql = @"
                SELECT DISTINCT v.*
                FROM vehicles v
                JOIN vehicle_units vu ON v.id = vu.vehicle_id
                WHERE vu.price_per_day >= @Min AND vu.price_per_day <= @Max
            ";
            var list = new List<Vehicle>();
            var db = new SqlDBHelper(_constr);
            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@Min", min);
            cmd.Parameters.AddWithValue("@Max", max);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new Vehicle
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Merk = reader.GetString(reader.GetOrdinal("merk")),
                    Category = Enum.Parse<vehicle_category_enum>(reader.GetString(reader.GetOrdinal("category"))),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Description = reader.IsDBNull(reader.GetOrdinal("description"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("description")),
                    Capacity = reader.GetInt32(reader.GetOrdinal("capacity")),
                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at"))
                                ? null
                                : reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at"))
                                ? null
                                : reader.GetDateTime(reader.GetOrdinal("updated_at")),
                });
            }

            db.CloseConnection();
            return list;
        }

        public int InsertVehicle(Vehicle vehicle)
        {
            if (string.IsNullOrEmpty(vehicle.Name))
                throw new ArgumentException("Nama kendaraan wajib diisi");

            if (string.IsNullOrEmpty(vehicle.Merk))
                throw new ArgumentException("Merk kendaraan wajib diisi");

            if (vehicle.Capacity <= 0)
                throw new ArgumentException("Kapasitas kendaraan harus lebih dari 0");

            var db = new SqlDBHelper(_constr);
            const string query = @"
                INSERT INTO vehicles (
                    merk, 
                    category, 
                    name, 
                    description, 
                    capacity, 
                    created_at, 
                    updated_at
                ) VALUES (
                    @Merk, 
                    @Category::vehicle_category_enum, 
                    @Name, 
                    @Description, 
                    @Capacity, 
                    CURRENT_TIMESTAMP, 
                    CURRENT_TIMESTAMP
                ) RETURNING id";

            try
            {
                using var cmd = db.GetNpgsqlCommand(query);

                cmd.Parameters.AddWithValue("@Merk", vehicle.Merk);
                cmd.Parameters.AddWithValue("@Category", vehicle.Category.ToString());
                cmd.Parameters.AddWithValue("@Name", vehicle.Name);
                cmd.Parameters.AddWithValue("@Description",
                    (object?)vehicle.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Capacity", vehicle.Capacity);

                var result = cmd.ExecuteScalar();
                return result == null ? 0 : Convert.ToInt32(result);
            }
            catch (Npgsql.PostgresException ex)
            {
                // Handle specific database errors
                switch (ex.SqlState)
                {
                    case "23505": // unique_violation
                        throw new ArgumentException("Kendaraan dengan nama tersebut sudah ada");
                    default:
                        throw new Exception($"Database error: {ex.Message}");
                }
            }
            finally
            {
                db.CloseConnection();
            }
        }

        public bool UpdateVehicle(int id, Vehicle vehicle)
        {
            try
            {
                var db = new SqlDBHelper(_constr);
                const string sql = @"
                    UPDATE vehicles
                    SET merk = @Merk,
                        category = @Category::vehicle_category_enum,
                        name = @Name,
                        description = @Description,
                        capacity = @Capacity,
                        updated_at = NOW()
                    WHERE id = @Id
                ";

                using var cmd = db.GetNpgsqlCommand(sql);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Merk", vehicle.Merk);
                cmd.Parameters.AddWithValue("@Category", vehicle.Category.ToString());
                cmd.Parameters.AddWithValue("@Name", vehicle.Name);
                cmd.Parameters.AddWithValue("@Description", vehicle.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Capacity", vehicle.Capacity);

                int rowsAffected = cmd.ExecuteNonQuery();
                db.CloseConnection();
                return rowsAffected > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool DeleteVehicle(int id)
        {
            try
            {
                var db = new SqlDBHelper(_constr);
                const string sql = "DELETE FROM vehicles WHERE id = @Id";
                using var cmd = db.GetNpgsqlCommand(sql);
                cmd.Parameters.AddWithValue("@Id", id);

                int rowsAffected = cmd.ExecuteNonQuery();
                db.CloseConnection();
                return rowsAffected > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public List<Vehicle> FilterVehicles(
            vehicle_category_enum? category,
            string? merk,
            int? kapasitas,
            decimal? min,
            decimal? max,
            string? name)
        {
            // Jika ada filter harga, kita perlu menggunakan JOIN dengan vehicle_units
            if (min.HasValue || max.HasValue)
            {
                const string sql = @"
                SELECT DISTINCT v.id, v.merk, v.category, v.name, v.description,
                    v.capacity, v.created_at, v.updated_at
                FROM vehicles v
                JOIN vehicle_units vu ON v.id = vu.vehicle_id
                WHERE 1=1
            ";

                var whereClause = new List<string>();
                var parameters = new Dictionary<string, object>();

                if (category.HasValue)
                {
                    whereClause.Add("v.category = @Category::vehicle_category_enum");
                    parameters["@Category"] = category.Value.ToString();
                }

                if (!string.IsNullOrEmpty(merk))
                {
                    whereClause.Add("LOWER(v.merk) = LOWER(@Merk)");
                    parameters["@Merk"] = merk;
                }

                if (kapasitas.HasValue)
                {
                    whereClause.Add("v.capacity = @Capacity");
                    parameters["@Capacity"] = kapasitas.Value;
                }

                if (min.HasValue)
                {
                    whereClause.Add("vu.price_per_day >= @Min");
                    parameters["@Min"] = min.Value;
                }

                if (max.HasValue)
                {
                    whereClause.Add("vu.price_per_day <= @Max");
                    parameters["@Max"] = max.Value;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    whereClause.Add("v.name ILIKE @Name");
                    parameters["@Name"] = $"%{name}%";
                }

                var fullSql = sql;
                if (whereClause.Count > 0)
                {
                    fullSql += " AND " + string.Join(" AND ", whereClause);
                }

                var list = new List<Vehicle>();
                var db = new SqlDBHelper(_constr);
                using var cmd = db.GetNpgsqlCommand(fullSql);

                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value);
                }

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(MapVehicleFromReader(reader));
                }

                db.CloseConnection();
                return list;
            }
            else
            {
                // Jika tidak ada filter harga, gunakan query tanpa JOIN
                return GetAllVehicles().Where(v =>
                    (!category.HasValue || v.Category == category) &&
                    (string.IsNullOrEmpty(merk) || v.Merk.Equals(merk, StringComparison.OrdinalIgnoreCase)) &&
                    (!kapasitas.HasValue || v.Capacity == kapasitas) &&
                    (string.IsNullOrEmpty(name) || v.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
        }

        private Vehicle MapVehicleFromReader(NpgsqlDataReader reader)
        {
            return new Vehicle
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Merk = reader.GetString(reader.GetOrdinal("merk")),
                Category = Enum.Parse<vehicle_category_enum>(reader.GetString(reader.GetOrdinal("category"))),
                Name = reader.GetString(reader.GetOrdinal("name")),
                Description = reader.IsDBNull(reader.GetOrdinal("description")) ?
                    null : reader.GetString(reader.GetOrdinal("description")),
                Capacity = reader.GetInt32(reader.GetOrdinal("capacity")),
                CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at")) ?
                    null : reader.GetDateTime(reader.GetOrdinal("created_at")),
                UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ?
                    null : reader.GetDateTime(reader.GetOrdinal("updated_at"))
            };
        }
    }
}