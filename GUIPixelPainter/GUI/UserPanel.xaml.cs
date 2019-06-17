using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GUIPixelPainter.GUI
{
    /// <summary>
    /// Interaction logic for UsersPanel.xaml
    /// </summary>
    public partial class UserPanel : UserControl
    {
        private class User
        {
            public Guid internalId;
            public string name;
            public string proxyUrl;
            public string authKey;
            public string authToken;
            public bool isEnabled;
            public Status status;
        }

        private List<User> users = new List<User>();
        private bool ignoreEvents = false;

        public GUIDataExchange DataExchange { get; set; }

        public UserPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// return an immutable copy of users
        /// </summary>
        /// <returns></returns>
        public List<GUIUser> GetUsers()
        {
            List<GUIUser> converted = new List<GUIUser>();
            foreach (User user in users)
            {
                converted.Add(new GUIUser(user.internalId, user.name, user.proxyUrl, user.authKey, user.authToken, user.status, user.isEnabled));
            }
            return converted;
        }

        public Guid GetSelectedUserGuidIfAny()
        {
            if (userList.SelectedItem == null)
                return Guid.Empty;
            return Guid.Parse(((userList.SelectedItem as StackPanel).Children[1] as TextBlock).Text);
        }

        public void SetUserStatus(Guid id, Status status)
        {
            var user = GetUser(id);
            if (user == null)
                return;
            if (user.status == status) //HACK hacky fix of a bug where you can't change user data while the user is enabled. May still be a problem in rare cases
                return;
            user.status = status;
            UpdateUserList();
        }

        private User GetSelectedUser()
        {
            return GetUser(Guid.Parse(((userList.SelectedItem as StackPanel).Children[1] as TextBlock).Text));
        }

        private User GetUser(Guid id)
        {
            return users.Find((a) => a.internalId == id);
        }

        private bool UserExists(Guid id)
        {
            return users.Find((a) => a.internalId == id) != null;
        }

        private void OnNewUserClick(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;
            string name = "New user";
            Guid id = Guid.NewGuid();
            User user = new User() { name = name, internalId = id };
            users.Add(user);

            DataExchange.UpdateUsersFromGUI();
            UpdateUserList();
        }

        private void UpdateUserList()
        {
            //remove old users
            for (int i = userList.Items.Count - 1; i >= 0; i--)
            {
                StackPanel item = userList.Items[i] as StackPanel;
                Guid id = Guid.Parse((item.Children[1] as TextBlock).Text);
                if (!UserExists(id))
                    userList.Items.Remove(item);
            }

            //add new users
            foreach (User user in users)
            {
                bool exists = false;
                foreach (StackPanel item in userList.Items)
                {
                    if (Guid.Parse((item.Children[1] as TextBlock).Text) == user.internalId)
                    {
                        exists = true;
                        //update name and status of existing user
                        var itemText = (item.Children[0] as Label);
                        itemText.Content = user.name;
                        if (user.status == Status.CONNECTING || user.status == Status.OPEN)
                            itemText.Foreground = Brushes.Green;
                        else if (user.status == Status.CLOSEDDISCONNECT || user.status == Status.CLOSEDERROR)
                            itemText.Foreground = Brushes.Red;
                        else
                            itemText.Foreground = Brushes.Black;
                        break;
                    }
                }
                if (exists)
                    continue;

                Label label = new Label() { Content = user.name };
                TextBlock id = new TextBlock() { Text = user.internalId.ToString(), Visibility = Visibility.Collapsed };
                StackPanel panel = new StackPanel();
                panel.Children.Add(label);
                panel.Children.Add(id);
                userList.Items.Add(panel);
            }
            UpdateUserSettingsPanel();
        }

        private void UpdateUserSettingsPanel()
        {
            ignoreEvents = true;

            if (userList.SelectedItem == null)
            {
                userName.Text = string.Empty;
                userProxy.Text = string.Empty;
                authKey.Text = string.Empty;
                authToken.Text = string.Empty;
                enableUser.IsChecked = false;

                userName.IsEnabled = false;
                userProxy.IsEnabled = false;
                authKey.IsEnabled = false;
                authToken.IsEnabled = false;
                enableUser.IsEnabled = false;
                deleteUser.IsEnabled = false;
                userStatus.Content = "Status: ";

                ignoreEvents = false;
                return;
            }
            User selectedUser = GetSelectedUser();

            userName.Text = selectedUser.name;
            userProxy.Text = selectedUser.proxyUrl;
            authKey.Text = selectedUser.authKey;
            authToken.Text = selectedUser.authToken;
            enableUser.IsChecked = selectedUser.isEnabled;
            userStatus.Content = "Status: " + selectedUser.status.ToString();

            userName.IsEnabled = true;
            userProxy.IsEnabled = true;
            authKey.IsEnabled = true;
            authToken.IsEnabled = true;
            enableUser.IsEnabled = true;
            deleteUser.IsEnabled = true;

            ignoreEvents = false;
        }

        private void OnDeleteUserClick(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;
            User user = GetSelectedUser();
            users.Remove(user);
            DataExchange.UpdateUsersFromGUI();
            UpdateUserList();
        }

        private void OnUserSelection(object sender, SelectionChangedEventArgs e)
        {
            if (ignoreEvents)
                return;
            UpdateUserSettingsPanel();
        }

        private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;
            if (userList.SelectedIndex == -1)
                return;

            User selectedUser = GetSelectedUser();

            selectedUser.name = userName.Text;
            selectedUser.proxyUrl = userProxy.Text;
            selectedUser.authKey = authKey.Text;
            selectedUser.authToken = authToken.Text;
            DataExchange.UpdateUsersFromGUI();
            if (sender.Equals(userName))
                UpdateUserList();
        }

        private void OnEnableUser(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;

            if (userList.SelectedItem == null)
                return;
            GetSelectedUser().isEnabled = true;
            DataExchange.UpdateUsersFromGUI();
        }

        private void OnDisableUser(object sender, RoutedEventArgs e)
        {
            if (ignoreEvents)
                return;

            if (userList.SelectedItem == null)
                return;
            GetSelectedUser().isEnabled = false;
            DataExchange.UpdateUsersFromGUI();
        }
    }
}