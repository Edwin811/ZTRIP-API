using System;
using System.Collections.Generic;
using Npgsql;
using Z_TRIP.Helpers;

namespace Z_TRIP.Models.Contexts
{
    public class BookingContext
    {
        private readonly string _constr;
        public BookingContext(string constr) => _constr = constr;

        public List<Booking> GetAllBookings()
        {
            var list = new List<Booking>();
            var db = new SqlDBHelper(_constr);
            const string sql = @"
                SELECT id, user_id, vehicle_unit_id,
                       start_datetime, end_datetime,
                       request_date, status, status_updated_at,
                       status_note, transaction_id,
                       created_at, updated_at
                  FROM bookings
              ORDER BY id;";
            using var cmd = db.GetNpgsqlCommand(sql);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(Map(rd));
            db.CloseConnection();
            return list;
        }

        public Booking? GetBookingById(int id)
        {
            Booking? b = null;
            var db = new SqlDBHelper(_constr);
            const string sql = "SELECT * FROM bookings WHERE id = @Id;";
            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@Id", id);
            using var rd = cmd.ExecuteReader();
            if (rd.Read()) b = Map(rd);
            db.CloseConnection();
            return b;
        }

        public Booking? GetBookingByTransactionId(int transactionId)
        {
            Booking? b = null;
            var db = new SqlDBHelper(_constr);
            const string sql = "SELECT * FROM bookings WHERE transaction_id = @TxnId;";
            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@TxnId", transactionId);
            using var rd = cmd.ExecuteReader();
            if (rd.Read()) b = Map(rd);
            db.CloseConnection();
            return b;
        }

        public List<Booking> GetBookingsByStatus(string status)
        {
            var list = new List<Booking>();
            var db = new SqlDBHelper(_constr);
            const string sql = @"
                SELECT * FROM bookings
                 WHERE status = @Status::booking_status_enum
              ORDER BY start_datetime;";
            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@Status", status);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(Map(rd));
            db.CloseConnection();
            return list;
        }

        public List<Booking> GetVehicleSchedule(int vehicleUnitId)
        {
            var list = new List<Booking>();
            var db = new SqlDBHelper(_constr);
            const string sql = @"
                SELECT * FROM bookings
                 WHERE vehicle_unit_id = @Vid
              ORDER BY start_datetime;";
            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@Vid", vehicleUnitId);
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(Map(rd));
            db.CloseConnection();
            return list;
        }

        public Booking? CreateBooking(Booking input)
        {
            var db = new SqlDBHelper(_constr);
            const string sql = @"
                INSERT INTO bookings
                  (user_id, vehicle_unit_id, start_datetime, end_datetime,
                   request_date, status, status_updated_at, status_note,
                   transaction_id, created_at, updated_at)
                VALUES
                  (@Uid, @Vid, @Start, @End,
                   @Req, @Stat::booking_status_enum, NOW(), @Note,
                   @Txn, NOW(), NOW())
                RETURNING *;";
            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@Uid", input.UserId);
            cmd.Parameters.AddWithValue("@Vid", input.VehicleUnitId);
            cmd.Parameters.AddWithValue("@Start", input.StartDatetime);
            cmd.Parameters.AddWithValue("@End", input.EndDatetime);
            cmd.Parameters.AddWithValue("@Req", input.RequestDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Stat", input.Status.ToString());
            cmd.Parameters.AddWithValue("@Note", input.StatusNote ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Txn", input.TransactionId ?? (object)DBNull.Value);
            using var rd = cmd.ExecuteReader();
            Booking? created = rd.Read() ? Map(rd) : null;
            db.CloseConnection();
            return created;
        }

        public bool UpdateBooking(int id, Booking input)
        {
            var db = new SqlDBHelper(_constr);
            const string sql = @"
                UPDATE bookings SET
                  user_id = @Uid,
                  vehicle_unit_id = @Vid,
                  start_datetime = @Start,
                  end_datetime = @End,
                  request_date = @Req,
                  status = @Stat::booking_status_enum,
                  status_note = @Note,
                  transaction_id = @Txn,
                  status_updated_at = NOW(),
                  updated_at = NOW()
                WHERE id = @Id;";
            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Uid", input.UserId);
            cmd.Parameters.AddWithValue("@Vid", input.VehicleUnitId);
            cmd.Parameters.AddWithValue("@Start", input.StartDatetime);
            cmd.Parameters.AddWithValue("@End", input.EndDatetime);
            cmd.Parameters.AddWithValue("@Req", input.RequestDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Stat", input.Status.ToString());
            cmd.Parameters.AddWithValue("@Note", input.StatusNote ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Txn", input.TransactionId ?? (object)DBNull.Value);
            var affected = cmd.ExecuteNonQuery();
            db.CloseConnection();
            return affected > 0;
        }

        public bool UpdateStatusBooking(int id, string status)
        {
            var db = new SqlDBHelper(_constr);
            const string sql = @"
                UPDATE bookings
                   SET status = @Stat::booking_status_enum,
                       status_updated_at = NOW()
                 WHERE id = @Id;";
            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Stat", status);
            var affected = cmd.ExecuteNonQuery();
            db.CloseConnection();
            return affected > 0;
        }

        public bool DeleteBooking(int id)
        {
            var db = new SqlDBHelper(_constr);
            const string sql = "DELETE FROM bookings WHERE id = @Id;";
            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@Id", id);
            var affected = cmd.ExecuteNonQuery();
            db.CloseConnection();
            return affected > 0;
        }

        public List<Booking> GetBookingsByVehicleUnitAndDateRange(int vehicleUnitId, DateTime startDate, DateTime endDate)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
        SELECT id, user_id, vehicle_unit_id, start_datetime, end_datetime, 
               request_date, status, status_updated_at, status_note, transaction_id, 
               created_at, updated_at
        FROM bookings
        WHERE vehicle_unit_id = @VehicleUnitId
          AND status IN ('pending', 'approved', 'on_going')
          AND (
              (start_datetime <= @EndDate AND end_datetime >= @StartDate) -- overlap check
          )
        ORDER BY start_datetime ASC";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@VehicleUnitId", vehicleUnitId);
            cmd.Parameters.AddWithValue("@StartDate", startDate);
            cmd.Parameters.AddWithValue("@EndDate", endDate);

            using var reader = cmd.ExecuteReader();
            var bookings = new List<Booking>();

            while (reader.Read())
            {
                bookings.Add(Map(reader));
            }

            return bookings;
        }

        private Booking Map(NpgsqlDataReader rd)
        {
            return new Booking
            {
                Id = rd.GetInt32(rd.GetOrdinal("id")),
                UserId = rd.GetInt32(rd.GetOrdinal("user_id")),
                VehicleUnitId = rd.GetInt32(rd.GetOrdinal("vehicle_unit_id")),
                StartDatetime = rd.GetDateTime(rd.GetOrdinal("start_datetime")),
                EndDatetime = rd.GetDateTime(rd.GetOrdinal("end_datetime")),
                RequestDate = rd.IsDBNull(rd.GetOrdinal("request_date")) ?
                    null : rd.GetDateTime(rd.GetOrdinal("request_date")),
                Status = Enum.Parse<booking_status_enum>(rd.GetString(rd.GetOrdinal("status"))),
                StatusUpdatedAt = rd.GetDateTime(rd.GetOrdinal("status_updated_at")),
                StatusNote = rd.IsDBNull(rd.GetOrdinal("status_note")) ?
                    null : rd.GetString(rd.GetOrdinal("status_note")),
                TransactionId = rd.IsDBNull(rd.GetOrdinal("transaction_id")) ?
                    null : rd.GetInt32(rd.GetOrdinal("transaction_id")),
                CreatedAt = rd.GetDateTime(rd.GetOrdinal("created_at")),
                UpdatedAt = rd.GetDateTime(rd.GetOrdinal("updated_at"))
            };
        }

        public List<Booking> GetActiveBookingsByDateRange(DateTime startDate, DateTime endDate)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
            SELECT id, user_id, vehicle_unit_id, start_datetime, end_datetime, 
                   request_date, status, status_updated_at, status_note, transaction_id, 
                   created_at, updated_at
            FROM bookings
            WHERE status::text IN ('pending', 'approved', 'on_going')
              AND (start_datetime <= @EndDate AND end_datetime >= @StartDate)
            ORDER BY start_datetime ASC";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@StartDate", startDate);
            cmd.Parameters.AddWithValue("@EndDate", endDate);

            using var reader = cmd.ExecuteReader();
            var bookings = new List<Booking>();

            while (reader.Read())
            {
                bookings.Add(Map(reader));
            }

            db.CloseConnection();
            return bookings;
        }

        public List<Booking> GetBlockedSchedules(DateTime? startDate = null, DateTime? endDate = null, int? vehicleUnitId = null)
        {
            var db = new SqlDBHelper(_constr);
            string sql = @"
        SELECT * FROM bookings 
        WHERE status_note LIKE 'BLOCKED_BY_ADMIN:%'";

            List<NpgsqlParameter> parameters = new List<NpgsqlParameter>();

            // Tambahkan filter tanggal jika disediakan
            if (startDate.HasValue)
            {
                sql += " AND end_datetime >= @StartDate";
                parameters.Add(new NpgsqlParameter("@StartDate", startDate.Value));
            }

            if (endDate.HasValue)
            {
                sql += " AND start_datetime <= @EndDate";
                parameters.Add(new NpgsqlParameter("@EndDate", endDate.Value));
            }

            // Tambahkan filter unit kendaraan jika disediakan
            if (vehicleUnitId.HasValue)
            {
                sql += " AND vehicle_unit_id = @UnitId";
                parameters.Add(new NpgsqlParameter("@UnitId", vehicleUnitId.Value));
            }

            // Urutkan berdasarkan tanggal mulai
            sql += " ORDER BY start_datetime";

            using var cmd = db.GetNpgsqlCommand(sql);
            foreach (var param in parameters)
            {
                cmd.Parameters.Add(param);
            }

            List<Booking> list = new();
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(Map(rd));
            }
            return list;
        }

        public List<Booking> GetBookingsByVehicleName(string vehicleName, DateTime startDate, DateTime endDate)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
        SELECT b.* 
        FROM bookings b
        JOIN vehicle_units vu ON b.vehicle_unit_id = vu.id
        JOIN vehicles v ON vu.vehicle_id = v.id
        WHERE LOWER(v.name) LIKE LOWER(@VehicleName)
        AND b.start_datetime <= @EndDate 
        AND b.end_datetime >= @StartDate
        ORDER BY b.start_datetime ASC";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@VehicleName", $"%{vehicleName}%");
            cmd.Parameters.AddWithValue("@StartDate", startDate);
            cmd.Parameters.AddWithValue("@EndDate", endDate);

            var bookings = new List<Booking>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                bookings.Add(Map(reader));
            }

            return bookings;
        }

        public List<Booking> GetBookingsByUserId(int userId)
        {
            var list = new List<Booking>();
            var db = new SqlDBHelper(_constr);
            const string sql = @"
                SELECT id, user_id, vehicle_unit_id,
                       start_datetime, end_datetime,
                       request_date, status, status_updated_at,
                       status_note, transaction_id,
                       created_at, updated_at
                  FROM bookings
                 WHERE user_id = @UserId
              ORDER BY start_datetime DESC;";

            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@UserId", userId);

            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(Map(rd));

            db.CloseConnection();
            return list;
        }

        public List<Booking> GetBookingsByStatusAndUserId(string status, int userId)
        {
            var list = new List<Booking>();
            var db = new SqlDBHelper(_constr);
            const string sql = @"
        SELECT * FROM bookings
         WHERE status = @Status::booking_status_enum
           AND user_id = @UserId
      ORDER BY start_datetime;";

            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@UserId", userId);

            using var rd = cmd.ExecuteReader();
            while (rd.Read()) list.Add(Map(rd));

            db.CloseConnection();
            return list;
        }

        public bool UpdateStatusNote(int id, string note)
        {
            var db = new SqlDBHelper(_constr);
            const string sql = @"
        UPDATE bookings 
        SET status_note = @Note,
            status_updated_at = CURRENT_TIMESTAMP,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = @Id";

            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Note", note);

            int rowsAffected = cmd.ExecuteNonQuery();
            db.CloseConnection();
            return rowsAffected > 0;
        }

        public List<Booking> GetAllBlockedSchedules(int? vehicleUnitId = null)
        {
            var db = new SqlDBHelper(_constr);
            string sql;

            // Jika vehicleUnitId disediakan, filter berdasarkan unit tersebut
            if (vehicleUnitId.HasValue)
            {
                sql = @"
                    SELECT * FROM bookings 
                    WHERE status_note LIKE 'BLOCKED_BY_ADMIN:%' 
                    AND vehicle_unit_id = @UnitId
                    ORDER BY start_datetime";

                using var cmd = db.GetNpgsqlCommand(sql);
                cmd.Parameters.AddWithValue("@UnitId", vehicleUnitId.Value);
                return ReadBookings(cmd);
            }
            else
            {
                // Jika tidak disediakan vehicleUnitId, ambil semua blocked schedule
                sql = @"
                    SELECT * FROM bookings 
                    WHERE status_note LIKE 'BLOCKED_BY_ADMIN:%'
                    ORDER BY start_datetime";

                using var cmd = db.GetNpgsqlCommand(sql);
                return ReadBookings(cmd);
            }
        }

        private List<Booking> ReadBookings(NpgsqlCommand cmd)
        {
            List<Booking> list = new();
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(Map(rd));
            }
            return list;
        }

        public List<BlockedDateInfo> GetAllBlockedDates(DateTime? startDate = null, DateTime? endDate = null)
        {
            // Ambil semua jadwal yang diblokir
            var blockedSchedules = GetBlockedSchedules(startDate, endDate, null);

            // Inisialisasi kontext untuk data kendaraan
            var unitCtx = new VehicleUnitsContext(_constr);
            var vehicleCtx = new VehicleContext(_constr);

            // Konversi ke format yang diinginkan
            List<BlockedDateInfo> result = new List<BlockedDateInfo>();

            foreach (var booking in blockedSchedules)
            {
                // Kumpulkan informasi unit kendaraan
                var unit = unitCtx.GetVehicleUnitById(booking.VehicleUnitId);
                string vehicleName = "Unknown";
                string vehicleMerk = "Unknown";

                if (unit != null)
                {
                    var vehicle = vehicleCtx.GetVehicleById(unit.VehicleId);
                    if (vehicle != null)
                    {
                        vehicleName = vehicle.Name;
                        vehicleMerk = vehicle.Merk;
                    }
                }

                // Penanganan tanggal: untuk setiap hari dalam rentang booking yang diblokir,
                // tambahkan entry untuk menunjukkan bahwa unit tersebut diblokir pada hari itu
                DateTime currentDate = booking.StartDatetime.Date;
                DateTime bookingEndDate = booking.EndDatetime.Date; // Changed variable name here

                while (currentDate <= bookingEndDate) // And use the new name here
                {
                    result.Add(new BlockedDateInfo
                    {
                        Date = currentDate,
                        VehicleUnitId = booking.VehicleUnitId,
                        VehicleUnitCode = unit?.Code ?? "Unknown",
                        VehicleName = vehicleName,
                        VehicleMerk = vehicleMerk,
                        Note = booking.StatusNote?.Replace("BLOCKED_BY_ADMIN: ", ""),
                        BookingId = booking.Id
                    });

                    currentDate = currentDate.AddDays(1);
                }
            }

            return result;
        }

        // Class untuk menyimpan informasi tanggal yang diblokir
        public class BlockedDateInfo
        {
            public DateTime Date { get; set; }
            public int VehicleUnitId { get; set; }
            public string? VehicleUnitCode { get; set; }
            public string? VehicleName { get; set; }
            public string? VehicleMerk { get; set; }
            public string? Note { get; set; }
            public int BookingId { get; set; }
        }
    }
}