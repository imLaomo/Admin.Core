using ZhonTai.Admin.Domain.Document;

namespace ZhonTai.Admin.Services.Document.Dto
{
    public class DocumentAddGroupInput
    {
        /// <summary>
        /// �����ڵ�
        /// </summary>
        public long ParentId { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public DocumentTypeEnum Type { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// ��
        /// </summary>
        public bool? Opened { get; set; }
    }
}