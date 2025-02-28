namespace ApiGraphActivator
{
    public class EdgarExternalItem
    {
        public string? idField { get; set; }
        public string? titleField { get; set; }
        public string? companyField { get; set; }
        public string? urlField { get; set; }
        public string? reportDateField { get; set; }
        public string? formField { get; set; }
        public string? contentField { get; set; }

        public EdgarExternalItem(string id, string title, string company, string url, string reportDate, string form, string content)
        {
            idField = id;
            titleField = title;
            companyField = company;
            urlField = url;
            reportDateField = reportDate;
            formField = form;
            contentField = content;
        }
    }


}
