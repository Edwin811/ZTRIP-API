using System;
using System.Collections.Generic;
using System.Linq;
using Z_TRIP.Helpers;
using Npgsql;

namespace Z_TRIP.Models
{
    public class UsersContext
    {
        private readonly string _constr;

        public UsersContext(string connectionString)
        {
            _constr = connectionString;
        }

        public Users? GetUserByEmail(string email)
        {
            var db = new SqlDBHelper(_constr);
            const string sql = @"
                SELECT id, email, name, profile, ktp_image, sim_image, password, role, 
                       created_at, updated_at, is_verified 
                FROM users 
                WHERE LOWER(email) = LOWER(@email)";

            using var cmd = db.GetNpgsqlCommand(sql);
            cmd.Parameters.AddWithValue("@email", email.Trim());

            try
            {
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new Users
                    {
                        Id = reader.GetInt32(0),
                        Email = reader.GetString(1),
                        Name = reader.GetString(2),
                        Profile = reader.IsDBNull(3) ? null : (byte[])reader["profile"],
                        KtpImage = reader.IsDBNull(4) ? null : (byte[])reader["ktp_image"],
                        SimImage = reader.IsDBNull(5) ? null : (byte[])reader["sim_image"],
                        Password = reader.GetString(6),
                        Role = reader.GetBoolean(7),
                        CreatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        UpdatedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                        IsVerified = reader.GetBoolean(10)
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting user by email: {ex.Message}");
            }
            finally
            {
                db.CloseConnection();
            }
        }

        // Ambil user berdasarkan ID (atau null jika tidak ada)
        public Users? GetUserById(int id)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                SELECT id, email, name, profile, ktp_image, sim_image, password, role, created_at, updated_at, is_verified
                FROM users
                WHERE id = @Id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Id", id);

            try
            {
                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    var user = new Users
                    {
                        Id = reader.GetInt32(0),
                        Email = reader.GetString(1),
                        Name = reader.GetString(2),
                        Password = reader.GetString(6),
                        Role = reader.GetBoolean(7),
                        CreatedAt = reader.GetDateTime(8),
                        UpdatedAt = reader.GetDateTime(9),
                        IsVerified = reader.GetBoolean(10)
                    };

                    // Handle nullable byte arrays
                    if (!reader.IsDBNull(3))
                    {
                        // Get profile as byte array from PostgreSQL bytea
                        user.Profile = (byte[])reader.GetValue(3);
                    }

                    if (!reader.IsDBNull(4))
                    {
                        // Get KTP image as byte array
                        user.KtpImage = (byte[])reader.GetValue(4);
                    }

                    if (!reader.IsDBNull(5))
                    {
                        // Get SIM image as byte array
                        user.SimImage = (byte[])reader.GetValue(5);
                    }

                    return user;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetUserById: {ex.Message}");
                throw;
            }
            finally
            {
                db.CloseConnection();
            }
        }

        // Registrasi user baru
        public bool RegisterUser(Users user)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                INSERT INTO users (email, name, profile, password, role, created_at, updated_at)
                VALUES (@Email, @Name, @Profile, @Password, @Role, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)";

            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Email", user.Email);
            cmd.Parameters.AddWithValue("@Name", user.Name);
            cmd.Parameters.AddWithValue("@Profile", user.Profile ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Password", user.Password);
            cmd.Parameters.AddWithValue("@Role", user.Role);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Update KTP Image
        public bool UpdateKtpImage(int userId, byte[] imageData)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE users
                SET ktp_image = @KtpImage,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @UserId";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@KtpImage", imageData);
            cmd.Parameters.AddWithValue("@UserId", userId);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Update SIM Image
        public bool UpdateSimImage(int userId, byte[] imageData)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE users
                SET sim_image = @SimImage,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @UserId";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@SimImage", imageData);
            cmd.Parameters.AddWithValue("@UserId", userId);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Update user profile
        public bool UpdateUserProfile(int userId, Users user)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE users
                SET name = @Name,
                    profile = @Profile,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @UserId";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Name", user.Name);
            cmd.Parameters.AddWithValue("@Profile", user.Profile ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", userId);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Tambahkan ke UsersContext.cs
        public List<Users> GetAllUsers()
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                SELECT id, email, name, profile, ktp_image, sim_image, password, role, created_at, updated_at, is_verified
                FROM users
                ORDER BY created_at DESC";

            using var cmd = db.GetNpgsqlCommand(query);
            using var reader = cmd.ExecuteReader();

            var userList = new List<Users>();
            while (reader.Read())
            {
                var user = new Users
                {
                    Id = reader.GetInt32(0),
                    Email = reader.GetString(1),
                    Name = reader.GetString(2),
                    // Perbaikan disini: Mengakses profile sebagai byte[] bukan string
                    Profile = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3),
                    KtpImage = reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4),
                    SimImage = reader.IsDBNull(5) ? null : (byte[])reader.GetValue(5),
                    Password = reader.GetString(6),
                    Role = reader.GetBoolean(7),
                    CreatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    UpdatedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    IsVerified = reader.IsDBNull(10) ? false : reader.GetBoolean(10)
                };

                userList.Add(user);
            }

            db.CloseConnection();
            return userList;
        }

        // Tambahkan method ini ke UsersContext.cs
        public bool UpdatePassword(int userId, string newPassword)
        {
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE users
                SET password = @Password,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @UserId";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Password", hashedPassword);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Tambahkan method ini ke UsersContext.cs jika belum ada
        public int RegisterUserWithIdentity(Users user)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                INSERT INTO users 
                    (email, name, profile, ktp_image, sim_image, password, role, created_at, updated_at)
                VALUES 
                    (@Email, @Name, @Profile, @KtpImage, @SimImage, @Password, @Role, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                RETURNING id";

            // Hash password
            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Email", user.Email);
            cmd.Parameters.AddWithValue("@Name", user.Name);
            cmd.Parameters.AddWithValue("@Profile", user.Profile ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@KtpImage", user.KtpImage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SimImage", user.SimImage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Password", user.Password);
            cmd.Parameters.AddWithValue("@Role", user.Role);

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

        // Add this method to UsersContext class

        public List<Users> GetCustomers()
        {
            var allUsers = GetAllUsers();
            return allUsers.Where(u => !u.Role).ToList(); // Return only non-admin users
        }

        // Method untuk update nama saja
        public bool UpdateUserName(int userId, string name)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE users
                SET name = @Name,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @Id";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Id", userId);
            cmd.Parameters.AddWithValue("@Name", name);

            int rowsAffected = cmd.ExecuteNonQuery();
            return rowsAffected > 0;
        }

        // Method untuk update gambar profil sebagai byte array
        public bool UpdateProfileImage(int userId, byte[] imageData)
        {
            var db = new SqlDBHelper(_constr);
            const string query = @"
                UPDATE users
                SET profile = @Profile,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @UserId";

            using var cmd = db.GetNpgsqlCommand(query);
            cmd.Parameters.AddWithValue("@Profile", imageData);
            cmd.Parameters.AddWithValue("@UserId", userId);

            int rowsAffected = cmd.ExecuteNonQuery();
            db.CloseConnection();
            return rowsAffected > 0;
        }
    }
}
