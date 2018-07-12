using GitObjectDb.Migrations;
using GitObjectDb.Models;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace GitObjectDb.Tests.Migrations
{
    public abstract class Migration : AbstractModel, IMigration
    {
        protected Migration(IServiceProvider serviceProvider, Guid id, string name, bool canDowngrade, bool isIdempotent)
            : base(serviceProvider, id, name)
        {
            CanDowngrade = canDowngrade;
            IsIdempotent = isIdempotent;
        }

        public bool CanDowngrade { get; }

        public bool IsIdempotent { get; }

        public void Up()
        {
            Console.WriteLine("Up");
        }

        public void Down()
        {
            Console.WriteLine("Down");
        }
    }
}
