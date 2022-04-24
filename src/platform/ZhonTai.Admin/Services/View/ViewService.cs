using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZhonTai.Admin.Core.Attributes;
using ZhonTai.Admin.Core.Dto;
using ZhonTai.Admin.Domain.View;
using ZhonTai.Admin.Domain.View.Dto;
using ZhonTai.Admin.Services.View.Dto;
using ZhonTai.DynamicApi;
using ZhonTai.DynamicApi.Attributes;

namespace ZhonTai.Admin.Services.View
{
    /// <summary>
    /// ��ͼ����
    /// </summary>
    [DynamicApi(Area = "admin")]
    public class ViewService : BaseService, IViewService, IDynamicApi
    {
        private readonly IViewRepository _viewRepository;

        public ViewService(IViewRepository moduleRepository)
        {
            _viewRepository = moduleRepository;
        }

        /// <summary>
        /// ��ѯ��ͼ
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetAsync(long id)
        {
            var result = await _viewRepository.GetAsync<ViewGetOutput>(id);
            return ResultOutput.Ok(result);
        }

        /// <summary>
        /// ��ѯ�б�
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<IResultOutput> GetListAsync(string key)
        {
            var data = await _viewRepository
                .WhereIf(key.NotNull(), a => a.Path.Contains(key) || a.Label.Contains(key))
                .OrderBy(a => a.ParentId)
                .OrderBy(a => a.Sort)
                .ToListAsync<ViewListOutput>();

            return ResultOutput.Ok(data);
        }

        /// <summary>
        /// ����
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> AddAsync(ViewAddInput input)
        {
            var entity = Mapper.Map<ViewEntity>(input);
            var id = (await _viewRepository.InsertAsync(entity)).Id;

            return ResultOutput.Result(id > 0);
        }

        /// <summary>
        /// �޸�
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IResultOutput> UpdateAsync(ViewUpdateInput input)
        {
            if (!(input?.Id > 0))
            {
                return ResultOutput.NotOk();
            }

            var entity = await _viewRepository.GetAsync(input.Id);
            if (!(entity?.Id > 0))
            {
                return ResultOutput.NotOk("��ͼ�����ڣ�");
            }

            Mapper.Map(input, entity);
            await _viewRepository.UpdateAsync(entity);
            return ResultOutput.Ok();
        }

        /// <summary>
        /// ����ɾ��
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> DeleteAsync(long id)
        {
            var result = false;
            if (id > 0)
            {
                result = (await _viewRepository.DeleteAsync(m => m.Id == id)) > 0;
            }

            return ResultOutput.Result(result);
        }

        /// <summary>
        /// ɾ��
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IResultOutput> SoftDeleteAsync(long id)
        {
            var result = await _viewRepository.SoftDeleteAsync(id);

            return ResultOutput.Result(result);
        }

        /// <summary>
        /// ����ɾ��
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>

        public async Task<IResultOutput> BatchSoftDeleteAsync(long[] ids)
        {
            var result = await _viewRepository.SoftDeleteAsync(ids);

            return ResultOutput.Result(result);
        }

        /// <summary>
        /// ͬ��
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [Transaction]
        public async Task<IResultOutput> SyncAsync(ViewSyncInput input)
        {
            //��ѯ������ͼ
            var views = await _viewRepository.Select.ToListAsync();
            var names = views.Select(a => a.Name).ToList();
            var paths = views.Select(a => a.Path).ToList();

            //path����
            foreach (var view in input.Views)
            {
                view.Path = view.Path?.Trim();
            }

            //��������
            {
                var inputViews = (from a in input.Views where !(paths.Contains(a.Path) || names.Contains(a.Name)) select a).ToList();
                if (inputViews.Count > 0)
                {
                    var insertViews = Mapper.Map<List<ViewEntity>>(inputViews);
                    foreach (var insertView in insertViews)
                    {
                        if (insertView.Label.IsNull())
                        {
                            insertView.Label = insertView.Name;
                        }
                    }
                    insertViews = await _viewRepository.InsertAsync(insertViews);
                    views.AddRange(insertViews);
                }
            }

            //��������
            {
                var inputPaths = input.Views.Select(a => a.Path).ToList();
                var inputNames = input.Views.Select(a => a.Name).ToList();

                //�޸�
                var updateViews = (from a in views where inputPaths.Contains(a.Path) || inputNames.Contains(a.Name) select a).ToList();
                if (updateViews.Count > 0)
                {
                    foreach (var view in updateViews)
                    {
                        var inputView = input.Views.Where(a => a.Name == view.Name || a.Path == view.Path).FirstOrDefault();
                        if (view.Label.IsNull())
                        {
                            view.Label = inputView.Label ?? inputView.Name;
                        }
                        if (view.Description.IsNull())
                        {
                            view.Description = inputView.Description;
                        }
                        view.Name = inputView.Name;
                        view.Path = inputView.Path;
                        view.Enabled = true;
                    }
                }

                //����
                var disabledViews = (from a in views where (a.Path.NotNull() || a.Name.NotNull()) && (!inputPaths.Contains(a.Path) || !inputNames.Contains(a.Name)) select a).ToList();
                if (disabledViews.Count > 0)
                {
                    foreach (var view in disabledViews)
                    {
                        view.Enabled = false;
                    }
                }

                updateViews.AddRange(disabledViews);
                await _viewRepository.UpdateDiy.SetSource(updateViews)
                .UpdateColumns(a => new { a.Label, a.Name, a.Path, a.Enabled, a.Description })
                .ExecuteAffrowsAsync();
            }
            

            return ResultOutput.Ok();
        }
    }
}