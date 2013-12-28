Twitter Archive Eraser
================

Twitter Archive Eraser allows you delete the oldest tweets from your timeline, or erase your whole twitter archive if you would like so.

The application is the simplest possible, it works in 3 steps: authenticate with Twitter; select which tweets you want to delete; erase them!


### Check [http://martani.github.io/Twitter-Archive-Eraser/](http://martani.github.io/Twitter-Archive-Eraser/) for more information.

![Twitter Archive Eraser](http://1.bp.blogspot.com/-LCvlx4R6OYM/URuxyFaMuuI/AAAAAAAAENA/esruT56sJlc/s400/step1.png)

Update
----------

- **Ver 4.0**: 
      - You can now load the whole zip archive in one click without needing to add specific *.js files;
      - The application will track any tweets which were not deleted and offers to retry deleting them again or save them on disk for deletion in the future;
      - Filtering based on regular expressions: Retweets, mentions etc... You can get all these very easily with Regex based filtering;
      - Different statistical information collecting
      - Updated licence: please check the licence section below in this document.
		 
- **Ver 3.0**: 
     - Adds the possibility to delete tweets in parallel (up to 16 operations at a time) + filtering of tweets based on keywords

- **Ver 2.0**: 
     - Enables the use of the *.js archive files after the Twitter update the the archive structure.


Download executable
-------------------

You can download a working standalone version from here: [Twitter Archive Eraser.zip](Twitter%20Archive%20Eraser.zip?raw=true)
Or, download the installer which will install all the required software (.NET 4.0 etc.): [Twitter Archive Eraser Setup.zip](Twitter%20Archive%20Eraser%20Setup.zip?raw=true)


Licence
-----------
Notice the licence update coming with Ver 4.0 (c.f. [Licence.txt](Licence.txt)).

In a nutshell:

- You ***can*** always use Twitter Archive Eraser for ***personal use*** and ***distribute*** it as you wish.
- You ***cannot*** use Twitter Archive Eraser for ***commercial purposes*** nor ***derive*** works based on it. 

If you wish to contribute to Twitter Archive Eraser, you are always welcome to do so on this repository on github.

![CC-NC-ND](http://i.creativecommons.org/l/by-nc-nd/3.0/88x31.png)

Please review also *TLDRLegal* for more about the **Creative Commons Attribution NonCommercial NoDerivs (CC-NC-ND)** licence. [[Link](http://www.tldrlegal.com/license/creative-commons-attribution-noncommercial-noderivs-(cc-nc-nd)]

Building the code using Visual Studio
------------------------------------------------------

If you wish to buid the code yourself, you need to create a Twitter application here [https://dev.twitter.com](https://dev.twitter.com) and provide the `twitterConsumerKey` and `twitterConsumerSecret` parameters in `App.config`.
<pre>
	&lt;add key="twitterConsumerKey" value=""/>
	&lt;add key="twitterConsumerSecret" value=""/>
</pre>
