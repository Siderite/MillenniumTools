using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MillenniumTools
{
    public class RelayCommand<T> : ICommand
    {
        #region Fields

        protected Action<T> _execute = null;
        protected Predicate<T> _canExecute = null;

        #endregion // Fields

        #region Constructors

        protected RelayCommand()
        {
        }

        public RelayCommand(Action<T> execute)
            : this(execute, null)
        {
        }

        /// <summary>
        /// Creates a new command.
        /// </summary>
        /// <param name="execute">The execution logic.</param>
        /// <param name="canExecute">The execution status logic.</param>
        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");

            _execute = execute;
            _canExecute = canExecute;
        }

        #endregion // Constructors

        #region ICommand Members

        [DebuggerStepThrough]
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute((T)parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }

        #endregion // ICommand Members
    }

    public class RelayCommand : RelayCommand<object>
    {
        public RelayCommand(Action execute, Func<bool> canExecute=null)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");

            _execute = o=>execute();
            _canExecute = canExecute == null
                ? null
                : new Predicate<object>(o => canExecute());
        }
    }
}
