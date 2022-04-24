namespace ZhonTai.Admin.Services.Document.Dto
{
    public class DocumentUpdateContentInput
    {
        /// <summary>
        /// ���
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Html
        /// </summary>
        public string Html { get; set; }

        /// <summary>
        /// �汾
        /// </summary>
        public long Version { get; set; }
    }
}