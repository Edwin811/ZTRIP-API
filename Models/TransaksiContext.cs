// File: Models/TransaksiContext.cs
using System;
using System.Collections.Generic;
using Npgsql;
using Z_TRIP.Helpers;

namespace Z_TRIP.Models
{
    public class TransaksiContext
    {
        private readonly string _constr;

        public TransaksiContext(string constr)
        {
            _constr = constr;
        }

        // Ambil semua transaksi
        public List<Transaksi> GetAllTransaksi()
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                SELECT id, method, payment_image, payment_status, amount, created_at, updated_at
                FROM transactions
                ORDER BY created_at DESC";

            using var cmd = db.GetNpgsqlCommand(query);
            using var reader = cmd.ExecuteReader();

            var transaksiList = new List<Transaksi>();
            while (reader.Read())
            {
                transaksiList.Add(new Transaksi
                {
                    Id = reader.GetInt32(0),
                    Method = Enum.Parse<payment_method_enum>(reader.GetString(1)),
                    PaymentImage = reader.IsDBNull(2) ? null : (byte[])reader["payment_image"],
                    PaymentStatus = Enum.Parse<payment_status_enum>(reader.GetString(3)),
                    Amount = reader.GetDecimal(4),
                    CreatedAt = reader.GetDateTime(5),
                    UpdatedAt = reader.GetDateTime(6)
                });
            }

            return transaksiList;
        }

        // Ambil transaksi berdasarkan ID
        public Transaksi? GetTransaksiById(int id)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                SELECT id, method, payment_image, payment_status, amount, created_at, updated_at
                FROM transactions
                WHERE id = @Id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var transaksi = new Transaksi
                {
                    Id = reader.GetInt32(0),
                    Method = Enum.Parse<payment_method_enum>(reader.GetString(1)),
                    PaymentImage = reader.IsDBNull(2) ? null : (byte[])reader["payment_image"],
                    PaymentStatus = Enum.Parse<payment_status_enum>(reader.GetString(3)),
                    Amount = reader.GetDecimal(4),
                    CreatedAt = reader.GetDateTime(5),
                    UpdatedAt = reader.GetDateTime(6)
                };
                db.CloseConnection();
                return transaksi;
            }

            db.CloseConnection();
            return null;
        }

        // Tambah transaksi baru
        public int AddTransaksi(Transaksi transaksi)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                INSERT INTO transactions (
                    method, 
                    amount, 
                    payment_status, 
                    payment_image,
                    created_at, 
                    updated_at
                )
                VALUES (
                    @Method::payment_method_enum, 
                    @Amount, 
                    @PaymentStatus::payment_status_enum,
                    @PaymentImage,
                    CURRENT_TIMESTAMP, 
                    CURRENT_TIMESTAMP
                )
                RETURNING id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Method", transaksi.Method.ToString());
            cmd.Parameters.AddWithValue("@Amount", transaksi.Amount);
            cmd.Parameters.AddWithValue("@PaymentStatus", transaksi.PaymentStatus.ToString());
            cmd.Parameters.AddWithValue("@PaymentImage",
                transaksi.PaymentImage != null ? (object)transaksi.PaymentImage : DBNull.Value);

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

        // Update status transaksi
        public bool UpdateTransaksiStatus(int id, string status)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE transactions
                SET payment_status = @Status::payment_status_enum,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @Id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@Id", id);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Upload bukti pembayaran
        public bool UploadPaymentProof(int id, byte[] imageData)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE transactions
                SET payment_image = @PaymentImage,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @Id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@PaymentImage", imageData);
            cmd.Parameters.AddWithValue("@Id", id);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Hapus transaksi
        public bool DeleteTransaksi(int id)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"DELETE FROM transactions WHERE id = @Id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Id", id);

            int rowsAffected = cmd.ExecuteNonQuery();
            db.CloseConnection();
            return rowsAffected > 0;
        }

        // Update jumlah transaksi
        public bool UpdateTransaksiAmount(int id, decimal amount)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE transactions
                SET amount = @Amount,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @Id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@Id", id);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Get transaksi berdasarkan IDs
        public List<Transaksi> GetTransaksiByIds(List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return new List<Transaksi>();

            var db = new SqlDBHelper(_constr);

            // Build query dengan parameter untuk setiap ID
            var parameters = new List<NpgsqlParameter>();
            var paramNames = new List<string>();

            for (int i = 0; i < ids.Count; i++)
            {
                var paramName = $"@Id{i}";
                parameters.Add(new NpgsqlParameter(paramName, ids[i]));
                paramNames.Add(paramName);
            }

            var query = $@"
                SELECT id, method, payment_image, payment_status, amount, created_at, updated_at
                FROM transactions
                WHERE id IN ({string.Join(",", paramNames)})
                ORDER BY created_at DESC";

            using var cmd = db.GetNpgsqlCommand(query);
            foreach (var param in parameters)
            {
                cmd.Parameters.Add(param);
            }

            using var reader = cmd.ExecuteReader();
            var transaksiList = new List<Transaksi>();

            while (reader.Read())
            {
                transaksiList.Add(new Transaksi
                {
                    Id = reader.GetInt32(0),
                    Method = Enum.Parse<payment_method_enum>(reader.GetString(1)),
                    PaymentImage = reader.IsDBNull(2) ? null : (byte[])reader["payment_image"],
                    PaymentStatus = Enum.Parse<payment_status_enum>(reader.GetString(3)),
                    Amount = reader.GetDecimal(4),
                    CreatedAt = reader.GetDateTime(5),
                    UpdatedAt = reader.GetDateTime(6)
                });
            }

            return transaksiList;
        }
    }
}