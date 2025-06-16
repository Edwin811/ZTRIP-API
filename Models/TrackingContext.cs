using System;
using System.Collections.Generic;
using Npgsql;
using Z_TRIP.Helpers;

namespace Z_TRIP.Models
{
    public class TrackingContext
    {
        private readonly string _constr;

        public TrackingContext(string constr)
        {
            _constr = constr;
        }

        public List<Tracking> GetAll()
        {
            var list = new List<Tracking>();
            var db = new SqlDBHelper(_constr);
            string query = "SELECT * FROM tracking ORDER BY recorded_at DESC;";

            using var cmd = db.GetNpgsqlCommand(query);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new Tracking
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    BookingId = reader.GetInt32(reader.GetOrdinal("booking_id")),
                    Latitude = reader.GetString(reader.GetOrdinal("latitude")),
                    Longitude = reader.GetString(reader.GetOrdinal("longitude")),
                    RecordedAt = reader.IsDBNull(reader.GetOrdinal("recorded_at")) ?
                        null : reader.GetDateTime(reader.GetOrdinal("recorded_at"))
                });
            }

            db.CloseConnection();
            return list;
        }

        public List<Tracking> GetByBookingId(int bookingId)
        {
            var list = new List<Tracking>();
            var db = new SqlDBHelper(_constr);
            string query = "SELECT * FROM tracking WHERE booking_id = @booking_id ORDER BY recorded_at;";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@booking_id", bookingId);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new Tracking
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    BookingId = reader.GetInt32(reader.GetOrdinal("booking_id")),
                    Latitude = reader.GetString(reader.GetOrdinal("latitude")),
                    Longitude = reader.GetString(reader.GetOrdinal("longitude")),
                    RecordedAt = reader.IsDBNull(reader.GetOrdinal("recorded_at")) ? null : reader.GetDateTime(reader.GetOrdinal("recorded_at"))
                });
            }

            db.CloseConnection();
            return list;
        }

        public string Create(Tracking tracking)
        {
            try
            {
                var db = new SqlDBHelper(_constr);
                string query = @"
                    INSERT INTO tracking (booking_id, latitude, longitude, recorded_at)
                    VALUES (@booking_id, @latitude, @longitude, NOW());";

                using var cmd = db.GetNpgsqlCommand(query);
                cmd.Parameters.AddWithValue("@booking_id", tracking.BookingId);
                cmd.Parameters.AddWithValue("@latitude", tracking.Latitude);
                cmd.Parameters.AddWithValue("@longitude", tracking.Longitude);
                cmd.ExecuteNonQuery();
                db.CloseConnection();

                return "Berhasil";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public string Delete(int id)
        {
            try
            {
                var db = new SqlDBHelper(_constr);
                string query = "DELETE FROM tracking WHERE id = @id;";
                using var cmd = db.GetNpgsqlCommand(query);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                db.CloseConnection();
                return "Berhasil";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public Tracking? GetLatestByBookingId(int bookingId)
        {
            var db = new SqlDBHelper(_constr);
            string query = @"
        SELECT * FROM tracking 
        WHERE booking_id = @booking_id 
        ORDER BY recorded_at DESC
        LIMIT 1;";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@booking_id", bookingId);
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                var tracking = new Tracking
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    BookingId = reader.GetInt32(reader.GetOrdinal("booking_id")),
                    Latitude = reader.GetString(reader.GetOrdinal("latitude")),
                    Longitude = reader.GetString(reader.GetOrdinal("longitude")),
                    RecordedAt = reader.IsDBNull(reader.GetOrdinal("recorded_at")) ? null : reader.GetDateTime(reader.GetOrdinal("recorded_at"))
                };

                db.CloseConnection();
                return tracking;
            }

            db.CloseConnection();
            return null;
        }
    }
}
