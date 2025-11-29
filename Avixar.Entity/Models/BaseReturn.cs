namespace Avixar.Entity.Models
{
    /// <summary>
    /// Generic response wrapper for all service layer methods.
    /// Standardizes communication between Service and UI layers.
    /// </summary>
    /// <typeparam name="T">The type of data being returned</typeparam>
    public class BaseReturn<T>
    {
        /// <summary>
        /// Indicates whether the operation was successful
        /// </summary>
        public bool Status { get; set; }

        /// <summary>
        /// User-friendly message describing the result
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The actual data payload (null if Status is false)
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// Detailed error messages for debugging (optional)
        /// </summary>
        public List<string>? Errors { get; set; }

        /// <summary>
        /// Creates a successful response with data
        /// </summary>
        public static BaseReturn<T> Success(T data, string message = "Operation successful")
        {
            return new BaseReturn<T>
            {
                Status = true,
                Message = message,
                Data = data
            };
        }

        /// <summary>
        /// Creates a failed response with error message
        /// </summary>
        public static BaseReturn<T> Failure(string message, List<string>? errors = null)
        {
            return new BaseReturn<T>
            {
                Status = false,
                Message = message,
                Errors = errors
            };
        }
    }
}
