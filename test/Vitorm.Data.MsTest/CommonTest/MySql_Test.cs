﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using User = Vitorm.MsTest.MySql.User;
using Vitorm.DataProvider;
using System.ComponentModel.DataAnnotations.Schema;
using Vitorm.Sql;

namespace Vitorm.MsTest.MySql
{
    public class User : Vitorm.MsTest.UserBase
    {

        [System.ComponentModel.DataAnnotations.Key]
        [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public override int id { get; set; }
    }
}


namespace Vitorm.MsTest
{

    [TestClass]
    public partial class MySql_Test : UserTest<User>
    {

        [TestMethod]
        public void Test()
        {
            Init();

            Test_Get();
            Test_Query();
            Test_QueryJoin();
            Test_ToExecuteString();
            Test_ExecuteUpdate();
            Test_ExecuteDelete();
            Test_Create();
            Test_Update();
            Test_Delete();
            Test_DbContext();
        }

        public override User NewUser(int id, bool forAdd = false) => new User { id = forAdd ? 0 : id, name = "testUser" + id };



        public void Init()
        {
            using var dbContext = Data.DataProvider<User>()?.CreateDbContext() as SqlDbContext;


            dbContext.Drop<User>();
            dbContext.Create<User>();

            var users = new List<User> {
                    new User  {   name="u146", fatherId=4, motherId=6 },
                    new User  {   name="u246", fatherId=4, motherId=6 },
                    new User  {   name="u356", fatherId=5, motherId=6 },
                    new User  {   name="u400" },
                    new User  {   name="u500" },
                    new User  {   name="u600" },
                };

            dbContext.AddRange(users);

            users.ForEach(user => { user.birth = DateTime.Parse("2021-01-01 00:00:00").AddHours(user.id); });

            dbContext.UpdateRange(users);

            WaitForUpdate();

        }


    }
}
