namespace ZhonTai.Admin.Services.Document.Dto
{
    public class DocumentUpdateMenuInput : DocumentAddMenuInput
    {
        /// <summary>
        /// ���
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// �汾
        /// </summary>
        public long Version { get; set; }
    }
}