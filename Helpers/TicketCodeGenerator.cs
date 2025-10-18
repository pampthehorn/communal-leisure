namespace website.Helpers
{
    public static class TicketCodeGenerator
    {
        public static string GenerateUniqueTicketCode()
        {
  
            return Path.GetRandomFileName().Replace(".", "").Substring(0, 8).ToUpper();
        }
    }
}