using System;

namespace Z_TRIP.Exceptions
{
    // Untuk resource tidak ditemukan
    public class ResourceNotFoundException : Exception
    {
        public ResourceNotFoundException() : base() { }
        public ResourceNotFoundException(string message) : base(message) { }
        public ResourceNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    // Untuk validasi input
    public class ValidationException : Exception
    {
        public ValidationException() : base() { }
        public ValidationException(string message) : base(message) { }
        public ValidationException(string message, Exception innerException) : base(message, innerException) { }
    }

    // Untuk pembayaran
    public class PaymentException : Exception
    {
        public PaymentException() : base() { }
        public PaymentException(string message) : base(message) { }
        public PaymentException(string message, Exception innerException) : base(message, innerException) { }
    }

    // Untuk konflik booking
    public class BookingConflictException : Exception
    {
        public BookingConflictException() : base() { }
        public BookingConflictException(string message) : base(message) { }
        public BookingConflictException(string message, Exception innerException) : base(message, innerException) { }
    }
}