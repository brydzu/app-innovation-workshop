﻿using System;
using System.Threading.Tasks;
using ContosoFieldService.Models;
using ContosoFieldService.Services;
using FreshMvvm;
using MvvmHelpers;
using Xamarin.Forms;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MonkeyCache.FileStore;

namespace ContosoFieldService.ViewModels
{
    public class JobsViewModel : FreshBasePageModel
    {
        #region Bindable Properties 
        public ObservableRangeCollection<GroupedJobs> Jobs { get; set; }

        public bool IsRefreshing
        {
            get { return isRefreshing; }
            set { isRefreshing = value; RaisePropertyChanged(); }
        }

        public bool IsLoading
        {
            get { return isLoading; }
            set { isLoading = value; RaisePropertyChanged(); }
        }

        string searchText;
        public string SearchText
        {
            get
            {
                return searchText;
            }
            set
            {
                searchText = value;
                if (string.IsNullOrWhiteSpace(value))
                {
                    // Run ReloadData syncronously
                    ReloadData(true).GetAwaiter().GetResult();
                }
                else
                    Search.Execute(value);
            }
        }

        Job selectedJob;
        public Job SelectedJob
        {
            get
            {
                return selectedJob;
            }
            set
            {
                selectedJob = value;
                if (value != null)
                    JobSelected.Execute(value);
                RaisePropertyChanged();

            }
        }

        #endregion

        #region Bindable Commands
        public Command Refresh
        {
            get
            {
                return new Command(async () =>
                {
                    await ReloadData(false, true);
                });
            }
        }

        public Command<Job> JobSelected
        {
            get
            {
                return new Command<Job>(async (job) =>
                {
                    await CoreMethods.PushPageModel<JobDetailsViewModel>(selectedJob);
                });
            }
        }

        public Command Search
        {
            get
            {
                return new Command(async () =>
                {
                    var searchResults = await jobsApiService.SearchJobsAsync(SearchText);
                    Jobs.ReplaceRange(new List<GroupedJobs>
                    {
                        new GroupedJobs("Search Results", searchResults)
                    });
                });
            }
        }

        public Command AddJobClicked
        {
            get
            {
                return new Command(async () =>
                {
                    await CoreMethods.PushPageModel<CreateNewJobViewModel>(null, false, true);
                });
            }
        }

        #endregion

        #region Overrides

        public override async void Init(object initData)
        {
            base.Init(initData);
            Jobs = new ObservableRangeCollection<GroupedJobs>();
        }

        protected override async void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);

            if (Helpers.Settings.LoginViewShown == false)
                await CoreMethods.PushPageModel<LoginViewModel>(null, true, true);

            await ReloadData(true);
        }

        protected override async void ViewIsDisappearing(object sender, EventArgs e)
        {
            SelectedJob = null;
        }

        public override async void ReverseInit(object returnedData)
        {
            base.ReverseInit(returnedData);
            SelectedJob = null;

            if (returnedData is Job job && job.IsDeleted)
            {
                // Job got deleted
                // Reload data
                await ReloadData(false, true);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Reloads the data.
        /// </summary>
        /// <returns>The data.</returns>
        /// <param name="isSilent">If set to <c>true</c> no loading indicators will be shown.</param>
        /// <param name="force">If set to <c>true</c> cache will be ignored.</param>
        async Task ReloadData(bool isSilent = false, bool force = false)
        {
            IsRefreshing = !isSilent;
            IsLoading = true;

            try
            {
                // Inform user about missing connectivity but proceed as data could have been cached
                if (!Plugin.Connectivity.CrossConnectivity.Current.IsConnected)
                    await CoreMethods.DisplayAlert("Network Error", "No internet connectivity found", "OK");

                // Get jobs from server or cache
                var newJobs = await jobsApiService.GetJobsAsync(force);

                // Group jobs by JobStatus
                var groupedJobs = GroupJobs(newJobs);
                Jobs.ReplaceRange(groupedJobs);
            }
            // TODO: Handle Exceptions centralized in BaseViewModel
            catch (UriFormatException)
            {
                // No or invalid BaseUrl set in Constants.cs
                await CoreMethods.DisplayAlert(
                    "Backend Error",
                    "No backend connection has been specified or the specified URL is malformed.",
                    "Ok");
            }
            catch (ArgumentException)
            {
                // Backend not found at specified BaseUrl in Constants.cs or call limit reached
                await CoreMethods.DisplayAlert(
                    "Backend Error",
                    "Cannot communicate with specified backend. Maybe your call rate limit is exceeded.",
                    "Ok");
            }
            catch (Exception ex)
            {
                // Everything else
                await CoreMethods.DisplayAlert(
                    "Backend Error",
                    "An error occured while communicating with the backend. Please check your settings and try again.",
                    "Ok");
            }

            IsRefreshing = false;
            IsLoading = false;
        }

        IEnumerable<GroupedJobs> GroupJobs(List<Job> jobs)
        {
            // Group jobs by JobStatus
            var groupedJobs = new List<GroupedJobs>
            {
                new GroupedJobs("Waiting", jobs.Where(x => x.Status == JobStatus.Waiting)),
                new GroupedJobs("In Progress", jobs.Where(x => x.Status == JobStatus.InProgress)),
                new GroupedJobs("Complete", jobs.Where(x => x.Status == JobStatus.Complete)),
            };

            // Return groups that actually have items to the list
            return groupedJobs.Where(x => x.Any());
        }

        #endregion

        #region Private Fields
        JobsAPIService jobsApiService = new JobsAPIService();
        bool isRefreshing;
        bool isLoading;
        #endregion
    }
}
