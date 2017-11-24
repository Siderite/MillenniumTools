using MillenniumTools.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MillenniumTools
{
    public class GraphItem:ViewModelBase
    {
        private string _ip;
        private double _x;
        private double _y;
        private int _offset;
        private bool _blur;

        public virtual string Ip
        {
            get
            {
                return _ip;
            }
            set
            {
                if (_ip != value)
                {
                    _ip = value;
                    OnPropertyChanged("Ip");
                }
            }
        }

        public virtual double X
        {
            get
            {
                return _x;
            }
            set
            {
                if (_x != value)
                {
                    _x = value;
                    OnPropertyChanged("X");
                }
            }
        }
        public virtual double Y
        {
            get
            {
                return _y;
            }
            set
            {
                if (_y != value)
                {
                    _y = value;
                    OnPropertyChanged("Y");
                }
            }
        }
        public virtual int Offset
        {
            get
            {
                return _offset;
            }
            set
            {
                if (_offset != value)
                {
                    _offset = value;
                    OnPropertyChanged("Offset");
                }
            }
        }
        public virtual bool Blur
        {
            get
            {
                return _blur;
            }
            set
            {
                if (_blur != value)
                {
                    _blur = value;
                    OnPropertyChanged("Blur");
                }
            }
        }
    }

    public class GraphLine : GraphItem
    {
        private double _x2;
        private double _y2;
        private double? _dash;

        public virtual double X2
        {
            get
            {
                return _x2;
            }
            set
            {
                if (_x2 != value)
                {
                    _x2 = value;
                    OnPropertyChanged("X2");
                }
            }
        }
        public virtual double Y2
        {
            get
            {
                return _y2;
            }
            set
            {
                if (_y2 != value)
                {
                    _y2 = value;
                    OnPropertyChanged("Y2");
                }
            }
        }

        public virtual double? Dash
        {
            get
            {
                return _dash;
            }
            set
            {
                if (_dash != value)
                {
                    _dash = value;
                    OnPropertyChanged("Dash");
                }
            }
        }
    }

    public class GraphStart : GraphItem { }
    public class GraphEnd : GraphItem { }

    public class GraphText : GraphItem
    {
        private string _text;
        public string Text
        {
            get
            {
                return _text;
            }
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged("Text");
                }
            }
        }
    }

    public class GraphSpline : GraphLine {
    }
}
