using System;
using System.Collections.Generic;
using Zephyr.Core;
using System.Dynamic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Zephyr.Utils;
using Zephyr.Web.Areas.Mms.Common;

namespace Zephyr.Models
{
    public class sys_userService : ServiceBase<sys_user>
    {
        public object Login(JObject request) 
        {
            var UserCode = request.Value<string>("usercode");
            var Password = request.Value<string>("password");

            //�û���������
            if (String.IsNullOrEmpty(UserCode) || String.IsNullOrEmpty(Password))
                return new { status = "error", message = "�û��������벻��Ϊ�գ�" };

            //�û���������֤
            var result = this.GetModel(ParamQuery.Instance()
                            .AndWhere("UserCode", UserCode)
                            .AndWhere("Password", Password)
                            .AndWhere("IsEnable", true));

            if (result == null || String.IsNullOrEmpty(result.UserCode))
                return new { status = "error", message = "�û��������벻��ȷ��" };

            //���ÿ���еĵ�½����
            var loginer = new LoginerBase { UserCode = result.UserCode, UserName = result.UserName };

            var effectiveHours = ZConfig.GetConfigInt("LoginEffectiveHours");
            FormsAuth.SignIn(loginer.UserCode, loginer, 60 * effectiveHours);       

            //��½����
            this.UpdateUserLoginCountAndDate(UserCode); //�����û���½������ʱ��
            this.AppendLoginHistory(request);           //��ӵ�½����
            MmsService.LoginHandler(request);           //MMSϵͳ��������ҵ����

            //���ص�½�ɹ�
            return new { status = "success", message = "��½�ɹ���" };
        }

        public void UpdateUserLoginCountAndDate(string UserCode)
        {
            db.Sql(@"
update sys_user
set LoginCount = isnull(LoginCount,0) + 1
   ,LastLoginDate = getdate()
where UserCode = @0 "
                , UserCode).Execute();
        }

        public void AppendLoginHistory(JObject request)
        {
            var lanIP = ZHttp.ClientIP;
            var hostName = ZHttp.IsLanIP(lanIP) ? ZHttp.ClientHostName : string.Empty; //����������ͻ�ȡ����������ȡ��������Ӱ��Ч��

            var UserCode = request.Value<string>("usercode");
            var UserName = MmsHelper.GetUserName();
            var IP = request.Value<string>("ip");
            var City = request.Value<string>("city");
            if (IP != lanIP)
                IP = string.Format("{0}/{1}", IP, lanIP).Trim('/').Replace("::1", "localhost");

            var item = new sys_loginHistory();
            item.UserCode = UserCode;
            item.UserName = UserName;
            item.HostName = hostName;
            item.HostIP = IP;
            item.LoginCity = City;
            item.LoginDate = DateTime.Now;

            db.Insert<sys_loginHistory>("sys_loginHistory", item).AutoMap(x => x.ID).Execute();
        }

        public bool AuthorizeUserMenu(List<string> urls)
        {
            var UserCode = FormsAuth.GetUserData().UserCode;
            var result = db.Sql(string.Format(@"
select 1
from sys_roleMenuMap A
left join sys_userRoleMap B on B.RoleCode = A.RoleCode
left join sys_menu C on C.MenuCode = A.MenuCode
where B.UserCode = '{1}'
and C.URL in ('{0}')",string.Join("','",urls),UserCode)).QueryMany<int>();

            return result.Count > 0;
        }


        public Dictionary<string, object> GetDefaultUserSetttins()
        {
            var defaults = new Dictionary<string, object>();
            defaults.Add("theme", "default");
            defaults.Add("navigation", "accordion");
            defaults.Add("gridrows", "20");
            return defaults;
        }

        public Dictionary<string, object> GetCurrentUserSettings()
        {
            var result = new Dictionary<string,object>();
            var UserCode = FormsAuth.GetUserData<LoginerBase>().UserCode;
            //var config = db.Sql("select ConfigJSON from sys_user where UserCode=@0", UserCode).QuerySingle<string>();
            var settings = db.Sql("select * from sys_userSetting where UserCode=@0", UserCode).QueryMany<sys_userSetting>();

            foreach (var item in settings)
                result.Add(item.SettingCode, item.SettingValue);

            var defaults = GetDefaultUserSetttins();

            foreach (var item in defaults)
                if (!result.ContainsKey(item.Key)) result.Add(item.Key,item.Value);

            return result;
        }

        public void SaveCurrentUserSettings(JObject settings)
        {
            var UserCode = FormsAuth.GetUserData<LoginerBase>().UserCode;
            foreach(JProperty item in settings.Children())
            {
                var result = db.Update("sys_userSetting")
                    .Column("SettingValue", item.Value.ToString())
                    .Where("UserCode", UserCode)
                    .Where("SettingCode", item.Name)
                    .Execute();

                if (result <= 0)
                {
                    var model = new sys_userSetting();
                    model.UserCode = UserCode;
                    model.SettingCode = item.Name;
                    model.SettingValue = item.Value.ToString();
                    db.Insert<sys_userSetting>("sys_userSetting", model).AutoMap(x=>x.ID).Execute();
                }
            }
        }
 
        public List<dynamic> GetRoleMembers(string role)
        {
            var result = db.Sql(String.Format(@"
 select A1.UserName as MemberName ,A1.UserCode as MemberCode,'user' as MemberType
  from sys_user A1
 where A1.UserCode in (select B1.UserCode from sys_userRoleMap B1 where B1.RoleCode = '{0}')
union
 select A2.OrganizeName as MemberName ,A2.OrganizeCode as MemberCode,'organize' as MemberType
   from sys_organize A2
  where A2.OrganizeCode in (select B2.OrganizeCode from sys_organizeRoleMap B2 where B2.RoleCode = '{0}')", role)).QueryMany<dynamic>();
            return result;
        }

        public dynamic GetUserOrganize(string user)
        {
            var sql = String.Format(@"
select distinct A.OrganizeCode,A.OrganizeName,A.ParentCode
,(case when B.UserCode is null then 'false' else 'true' end) as Checked
from sys_organize A
left join sys_userOrganizeMap B on B.OrganizeCode = A.OrganizeCode and B.UserCode = '{0}'", user);
            return db.Sql(sql).QueryMany<dynamic>();
        }

        public void SaveUserOrganizes(string UserCode, JToken OrganizeList)
        {
            db.UseTransaction(true);
            Logger("�����û�����", () =>
            {
                db.Delete("sys_userOrganizeMap").Where("UserCode", UserCode).Execute();
                foreach (JToken item in OrganizeList.Children())
                {
                    var OrganizeCode = item["OrganizeCode"].ToString();
                    db.Insert("sys_userOrganizeMap").Column("UserCode", UserCode).Column("OrganizeCode", OrganizeCode).Execute();
                }
                db.Commit();
            }, e => db.Rollback());
        }

        public dynamic GetUserRole(string user)
        {
            var sql = String.Format(@"
select distinct A.RoleCode,A.RoleName
,(case when B.RoleCode is null then 'false' else 'true' end) as Checked
from sys_role A
left join sys_userRoleMap B on B.RoleCode = A.RoleCode and B.UserCode = '{0}'", user);
            return db.Sql(sql).QueryMany<dynamic>();
        }

        public void SaveUserRoles(string UserCode, JToken RoleList)
        {
            db.UseTransaction(true);
            Logger("�����û���ɫ", () =>
            {
                db.Delete("sys_userRoleMap").Where("UserCode", UserCode).Execute();
                foreach (JToken item in RoleList.Children())
                {
                    var RoleCode = item["RoleCode"].ToString();
                    db.Insert("sys_userRoleMap").Column("UserCode", UserCode).Column("RoleCode", RoleCode).Execute();
                }
                db.Commit();
            }, e => db.Rollback());
        }

        public int ResetUserPassword(string UserCode)
        {
            var defaultPassword = "1234";
            var result = db.Update("sys_user")
                .Column("Password", defaultPassword)
                .Where("UserCode", UserCode)
                .Execute();
            return result;
        }

        protected override void OnAfterEditDetail(EditEventArgs arg)
        {
            if (arg.type == OptType.Add)
            {
                ResetUserPassword(arg.row["UserCode"].ToString());
            }
            base.OnAfterEditDetail(arg);
        }
    }
    
    public class sys_user : ModelBase
    {

        [PrimaryKey]
        public string UserCode
        {
            get;
            set;
        }

        public string UserSeq
        {
            get;
            set;
        }

        public string UserName
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public string Password
        {
            get;
            set;
        }

        public string RoleName
        {
            get;
            set;
        }

        public string OrganizeName
        {
            get;
            set;
        }

        public string ConfigJSON
        {
            get;
            set;
        }

        public bool? IsEnable
        {
            get;
            set;
        }

        public int? LoginCount
        {
            get;
            set;
        }

        public DateTime? LastLoginDate
        {
            get;
            set;
        }

        public string CreatePerson
        {
            get;
            set;
        }

        public DateTime? CreateDate
        {
            get;
            set;
        }

        public string UpdatePerson
        {
            get;
            set;
        }

        public DateTime? UpdateDate
        {
            get;
            set;
        }

    }
}
