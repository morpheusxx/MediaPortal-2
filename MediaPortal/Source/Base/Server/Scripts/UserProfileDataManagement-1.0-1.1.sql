-- This script updates the UserProfileDataManagement schema from version 1.0 to version 1.1. DO NOT MODIFY!

ALTER TABLE USER_PROFILES ADD PASSWORD %STRING(250)%;
ALTER TABLE USER_PROFILES ADD IMAGE %BINARY%;
ALTER TABLE USER_PROFILES ADD LAST_LOGIN %TIMESTAMP%;