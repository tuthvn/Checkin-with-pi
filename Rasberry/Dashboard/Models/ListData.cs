using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Models
{
    public class ListData
    {
        public string texts { get; set; }
    }
    public static class ListDataModal
    {
        public static List<ListData> listTempSource = new List<ListData>();
        public static List<ListData> setListDataSource()
        {
            listTempSource.Add(new ListData { texts = "Inbox" });
            listTempSource.Add(new ListData { texts = "VIP" });
            listTempSource.Add(new ListData { texts = "Drafts" });
            listTempSource.Add(new ListData { texts = "Sent" });
            listTempSource.Add(new ListData { texts = "Junk" });
            listTempSource.Add(new ListData { texts = "Trash" });
            listTempSource.Add(new ListData { texts = "All mails" });
            listTempSource.Add(new ListData { texts = "Mail" });
            return listTempSource;
        }
    }
}
