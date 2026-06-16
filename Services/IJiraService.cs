using XbrlSupportBot.Models;

namespace XbrlSupportBot.Services
{
    public interface IJiraService
    {
        Task<List<JiraTicket>> FetchRcaTicketsAsync();

        Task<List<JiraTicket>> FetchRcaTicketsPostAsync();
    }
}