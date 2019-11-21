using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace GUIPixelPainter.GUI
{
    struct ListElement
    {
        public string title;
        public Color color;
    }

    class ElementListViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<ListElement> elements;

        public ElementListViewModel()
        {
            elements = new ObservableCollection<ListElement>();
        }

        private ListElement _selectedElement;
        public ListElement SelectedElement
        {
            get { return _selectedElement; }
            set
            {
                _selectedElement = value;
                RaisePropertyChanged(nameof(SelectedElement));
            }
        }

        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}
