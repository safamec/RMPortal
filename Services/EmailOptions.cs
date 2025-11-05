namespace RMPortal.Services
{
    public sealed class EmailOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;

        // Gmail App Password setup
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";

        public string FromAddress { get; set; } = "";
        public string FromName { get; set; } = "RMPortal";

        // Optional pickup folder for local dev
        public bool UsePickupFolder { get; set; } = false;
        public string PickupFolder { get; set; } = "C:\\maildrop";
    }
}
