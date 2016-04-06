﻿using System.ComponentModel.DataAnnotations.Schema;

namespace NxtWallet.Model
{
    [Table("Setting")]
    public class SettingDto
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
    }
}