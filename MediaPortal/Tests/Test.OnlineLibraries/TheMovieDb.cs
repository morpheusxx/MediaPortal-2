﻿using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MediaPortal.Common;
using MediaPortal.Common.Localization;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement.Helpers;
using MediaPortal.Common.PathManager;
using MediaPortal.Common.Services.PathManager;
using MediaPortal.Extensions.OnlineLibraries;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.OnlineLibraries
{
  [TestClass]
  public class TheMovieDb
  {
    [TestMethod]
    public void TestMovieDbMatches()
    {
      string testDataLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
      string cacheLocation = Path.Combine(testDataLocation, "TheMovieDB");
      // Delete cache to force online lookups
      if (Directory.Exists(cacheLocation))
        Directory.Delete(cacheLocation, true);

      PathManager pathManager = new PathManager();
      pathManager.SetPath("DATA", testDataLocation);

      ServiceRegistration.Set<IPathManager>(pathManager);
      ServiceRegistration.Set<ILocalization>(new NoLocalization());
      ServiceRegistration.Set<ILogger>(new NoLogger());

      // List of movie titles which are expected to be matched unique
      List<MovieInfo> shouldMatchMovies = new List<MovieInfo>
      {
        new MovieInfo { MovieName = "Total Recall", Year = 2012 },
        new MovieInfo { MovieName = "Android Insurrection" },
        new MovieInfo { MovieName = "Aliens vs. Avatars" },
        new MovieInfo { MovieName = "Princess of Mars" },
        new MovieInfo { MovieName = "Just Sex & Nothing else" },
        new MovieInfo { MovieName = "Strictly Sexual - Endlich Sex!!" },
        new MovieInfo { MovieName = "Reality XL" },
        new MovieInfo { MovieName = "Werner - Eiskalt" },
        new MovieInfo { MovieName = "John Carter - Zwischen Zwei Welten" },
        new MovieInfo { MovieName = "Ich - Einfach Unverbesserlich" },
        new MovieInfo { MovieName = "Battleship" },
        new MovieInfo { MovieName = "Marvel's The Avengers" },
        new MovieInfo { MovieName = "Men In Black 3" },
        new MovieInfo { MovieName = "Thor", Year = 2011 },
        new MovieInfo { MovieName = "Zorn der Titanen" },
        new MovieInfo { MovieName = "Ziemlich beste Freunde" },
        new MovieInfo { MovieName = "Willkommen bei den Sch'tis" },
        new MovieInfo { MovieName = "Whistleblower - In gefährlicher Mission" },
        new MovieInfo { MovieName = "Unbeugsam - Defiance" },
        new MovieInfo { MovieName = "Transformers" },
        new MovieInfo { MovieName = "Transformers - Die Rache" },
        new MovieInfo { MovieName = "Three Inches" },
        new MovieInfo { MovieName = "The Watch - Nachbarn der 3. Art" },
        new MovieInfo { MovieName = "The Reef - Schwimm um dein Leben" },
        new MovieInfo { MovieName = "The Man with the Iron Fists" },
        new MovieInfo { MovieName = "The King's Speech - Die Rede des Königs" },
        new MovieInfo { MovieName = "The Expendables 2 - Uncut Version" },
        new MovieInfo { MovieName = "The Dark Knight Rises" },
        new MovieInfo { MovieName = "The Artist" },
        new MovieInfo { MovieName = "Terminator 3 - Rebellion der Maschinen" },
        new MovieInfo { MovieName = "Ted" },
        new MovieInfo { MovieName = "Spider City - Stadt der Spinnen" },
        new MovieInfo { MovieName = "Snow White and the Huntsman" },
        new MovieInfo { MovieName = "Schattenwelt" },
        new MovieInfo { MovieName = "Salt", Year = 2010 },
        new MovieInfo { MovieName = "Resident Evil: Damnation" },
        new MovieInfo { MovieName = "Rango" },
        new MovieInfo { MovieName = "Quarantäne" },
        new MovieInfo { MovieName = "Prometheus - Dunkle Zeichen" },
        new MovieInfo { MovieName = "Prince of Persia: Der Sand der Zeit" },
        new MovieInfo { MovieName = "Premium Rush" },
        new MovieInfo { MovieName = "Predators" },
        new MovieInfo { MovieName = "Planet der Affen: Prevolution" },
        new MovieInfo { MovieName = "Pirates of the Caribbean - Fremde Gezeiten" },
        new MovieInfo { MovieName = "Paranorman" },
        new MovieInfo { MovieName = "Outlander" },
        new MovieInfo { MovieName = "Oben" },
        new MovieInfo { MovieName = "Männertrip" },
        new MovieInfo { MovieName = "Merida - Legende der Highlands" },
        new MovieInfo { MovieName = "Mann tut was Mann kann" },
        new MovieInfo { MovieName = "Liebe, Sex und Seitensprünge" },
        new MovieInfo { MovieName = "Knight and Day" },
        new MovieInfo { MovieName = "Kiss & Kill" },
        new MovieInfo { MovieName = "Jennifers Body - Jungs nach ihrem Geschmack" },
        new MovieInfo { MovieName = "James Bond 007 - Skyfall" },
        new MovieInfo { MovieName = "Iron Sky" },
        new MovieInfo { MovieName = "Indiana Jones und das Königreich des Kristallschädels" },
        new MovieInfo { MovieName = "Illuminati" },
        new MovieInfo { MovieName = "Ich bin Nummer Vier" },
        new MovieInfo { MovieName = "Ice Age 4 - Voll verschoben" },
        new MovieInfo { MovieName = "Hotel Transsilvanien" },
        new MovieInfo { MovieName = "Harry Potter und die Kammer des Schreckens" },
        new MovieInfo { MovieName = "Harry Potter und die Heiligtümer des Todes (2)" },
        new MovieInfo { MovieName = "Harry Potter und der Halbblutprinz" },
        new MovieInfo { MovieName = "Harry Potter und der Gefangene von Askaban" },
        new MovieInfo { MovieName = "Hangover" },
        new MovieInfo { MovieName = "Hangover 2" },
        new MovieInfo { MovieName = "Gnomeo und Julia" },
      };
      // List of movie titles which are known to lead to false matching (i.e. only one result is returned, but is wrong)
      // At current code base, the matches are not considered as valid, as there is a similarity check.
      List<MovieInfo> shouldNotMatchMovies = new List<MovieInfo>
      {
        new MovieInfo { MovieName = "Weissensee" },
        new MovieInfo { MovieName = "Landpartie" },
      };

      MovieTheMovieDbMatcher matcher = new MovieTheMovieDbMatcher { DownloadFanart = false };
      matcher.Init();

      foreach (MovieInfo movieInfo in shouldMatchMovies)
      {
        bool match = matcher.FindAndUpdateMovie(movieInfo);
        Assert.IsTrue(match, string.Format("Failed to look up '{0}'", movieInfo.MovieName));
      }

      foreach (MovieInfo movieInfo in shouldNotMatchMovies)
      {
        string originalName = movieInfo.MovieName;
        bool match = matcher.FindAndUpdateMovie(movieInfo);
        Assert.IsFalse(match, string.Format("Wrong online look up for '{0}' --> '{1}', should not be matched!", originalName, movieInfo.MovieName));
      }
    }
  }
}
