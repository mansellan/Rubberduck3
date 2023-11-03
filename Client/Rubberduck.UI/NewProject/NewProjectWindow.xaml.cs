﻿using System.Windows;

namespace Rubberduck.UI.NewProject
{
    /// <summary>
    /// Interaction logic for NewProjectWindow.xaml
    /// </summary>
    public partial class NewProjectWindow : Window
    {
        public NewProjectWindow(NewProjectWindowViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        public NewProjectWindow()
        {
            InitializeComponent();
        }
    }
}