using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace MvcWebCrawler.Data
{
    public class FavoriteRepository
    {
        private readonly string _filePath;
        private static readonly object _lockObject = new object();

        public FavoriteRepository()
        {
            try
            {
                // 資料存放在 App_Data 資料夾的 JSON 檔案
                string appDataPath = HttpContext.Current.Server.MapPath("~/App_Data");
                System.Diagnostics.Debug.WriteLine($"App_Data path: {appDataPath}");
                
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                    System.Diagnostics.Debug.WriteLine($"Created App_Data directory");
                }
                
                _filePath = Path.Combine(appDataPath, "favorites.json");
                System.Diagnostics.Debug.WriteLine($"Favorites file path: {_filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in FavoriteRepository constructor: {ex.Message}");
                throw;
            }
        }

        private List<FavoriteItem> LoadFavorites()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!File.Exists(_filePath))
                    {
                        System.Diagnostics.Debug.WriteLine("Favorites file does not exist, returning empty list");
                        return new List<FavoriteItem>();
                    }

                    string json = File.ReadAllText(_filePath);
                    System.Diagnostics.Debug.WriteLine($"Loaded JSON: {json}");
                    
                    var favorites = JsonConvert.DeserializeObject<List<FavoriteItem>>(json) ?? new List<FavoriteItem>();
                    System.Diagnostics.Debug.WriteLine($"Loaded {favorites.Count} favorites");
                    
                    return favorites;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading favorites: {ex.Message}");
                    return new List<FavoriteItem>();
                }
            }
        }

        private void SaveFavorites(List<FavoriteItem> favorites)
        {
            lock (_lockObject)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(favorites, Formatting.Indented);
                    System.Diagnostics.Debug.WriteLine($"Saving JSON: {json}");
                    
                    File.WriteAllText(_filePath, json);
                    System.Diagnostics.Debug.WriteLine($"Saved {favorites.Count} favorites to {_filePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving favorites: {ex.Message}");
                    throw;
                }
            }
        }

        public List<string> GetAllFavoriteCompanyIds()
        {
            try
            {
                var favorites = LoadFavorites();
                var ids = favorites.OrderByDescending(f => f.CreatedDate).Select(f => f.CompanyId).ToList();
                System.Diagnostics.Debug.WriteLine($"GetAllFavoriteCompanyIds returning {ids.Count} items");
                return ids;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAllFavoriteCompanyIds: {ex.Message}");
                return new List<string>();
            }
        }

        public bool AddFavorite(string companyId, string companyName = "")
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"AddFavorite: {companyId}, {companyName}");
                
                var favorites = LoadFavorites();
                
                // 檢查是否已存在
                if (favorites.Any(f => f.CompanyId == companyId))
                {
                    System.Diagnostics.Debug.WriteLine($"Favorite already exists: {companyId}");
                    return true; // 已存在，視為成功
                }

                favorites.Add(new FavoriteItem
                {
                    CompanyId = companyId,
                    CompanyName = companyName,
                    CreatedDate = DateTime.Now
                });

                SaveFavorites(favorites);
                System.Diagnostics.Debug.WriteLine($"Successfully added favorite: {companyId}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddFavorite: {ex.Message}");
                return false;
            }
        }

        public bool RemoveFavorite(string companyId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"RemoveFavorite: {companyId}");
                
                var favorites = LoadFavorites();
                var itemToRemove = favorites.FirstOrDefault(f => f.CompanyId == companyId);
                
                if (itemToRemove != null)
                {
                    favorites.Remove(itemToRemove);
                    SaveFavorites(favorites);
                    System.Diagnostics.Debug.WriteLine($"Successfully removed favorite: {companyId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Favorite not found: {companyId}");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RemoveFavorite: {ex.Message}");
                return false;
            }
        }

        public bool IsFavorite(string companyId)
        {
            try
            {
                var favorites = LoadFavorites();
                var isFav = favorites.Any(f => f.CompanyId == companyId);
                System.Diagnostics.Debug.WriteLine($"IsFavorite({companyId}): {isFav}");
                return isFav;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in IsFavorite: {ex.Message}");
                return false;
            }
        }
    }

    public class FavoriteItem
    {
        public string CompanyId { get; set; }
        public string CompanyName { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
