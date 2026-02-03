# Meal Plan Organizer - Application Specification

## Project Overview

**Meal Plan Organizer** is a family-focused mobile application designed to help household members collaborate on rating recipes, creating meal plans, and discovering new recipes based on preferences and available ingredients.

**Target Users**: 2 adults and 2 teens in a household

**Budget Constraint**: Azure monthly spend must remain below $50/month

## Core Features

### 1. Recipe Management
- **Add/View Recipes**: Users can add new recipes with:
  - Recipe name
  - Ingredients list
  - Cooking instructions
  - Cuisine type/category
  - Prep time and cook time
  - Servings
  - Images/photos
- **GenAI Recipe Extraction**: Import recipes automatically from:
  - Uploaded images (cookbook photos, handwritten recipes, screenshots)
  - Recipe URLs (links to recipe websites)
  - Pasted text (copied from websites or documents)
  - AI extracts: name, ingredients with quantities/units, instructions, times, servings
  - User can review and edit extracted data before saving
- **Recipe Rating System**: 
  - Family members can rate recipes on a 1-5 star scale
  - Optional review/comments
  - Track rating history over time

### 2. Meal Planning
- **Create Meal Plans**: Build weekly or custom-duration meal plans
- **Assign Recipes to Days**: Map recipes to breakfast, lunch, dinner, or snacks
- **View Shopping Lists**: Generate shopping lists from selected recipes
- **Share with Family**: All household members can view and suggest meal plans

### 3. Recipe Recommendation Engine
- **Content-Based Recommendations**: 
  - Suggest recipes similar to those rated highly
  - Consider cuisine type, ingredients, and preparation time
- **Ingredient-Based Recommendations**:
  - Suggest recipes based on ingredients the family has on hand
  - Recommend recipes that use available pantry items
- **Popularity-Based Recommendations**:
  - Track which recipes are most frequently used in meal plans
  - Recommend frequently-used recipe variations

### 4. Mobile Experience
- **Native Mobile Apps**: .NET MAUI applications for iOS and Android
- **Offline Capability**: Core features should work offline with sync when reconnected
- **Mobile-First Design**: Optimize for touch interaction and smaller screens
- **Real-Time Updates**: Family members see recipe ratings and meal plan changes promptly

## Technical Stack

### Frontend
- **Framework**: .NET MAUI
- **Platforms**: iOS and Android
- **Language**: C#
- **Architecture**: MVVM or equivalent pattern

### Backend
- **Cloud Platform**: Microsoft Azure
- **Primary Services** (to stay within $50/month budget):
  - **Azure Functions**: API backend
  - **Azure SQL Database**: Primary data store
  - **Azure Storage**: For recipe images
  - **Azure Service Bus/SignalR**: Real-time notifications and updates (optional, for enhanced features)
  - **Microsoft Entra External ID**: User authentication and management (CIAM in external tenant)
  - **Azure OpenAI**: GPT-4o with Vision for GenAI recipe extraction (~$1.00/month)
  - **Azure AI Vision**: OCR Read API for image text extraction (~$0.08/month)

### Architecture Approach
- RESTful API backend (or GraphQL as alternative)
- Stateless API design for scalability
- API versioning for future compatibility
- JWT/OAuth2 for authentication

## Data Model Overview

### Core Entities
- **User/Family Member**
  - Name, email, authentication credentials
  - Role (admin/member)
  - Preferences (dietary restrictions, cuisine preferences)

- **Recipe**
  - Name, description, cuisine category
  - Ingredients (with quantities and units)
  - Instructions
  - Images
  - Metadata (prep time, cook time, servings)
  - Created by (user)
  - Created date

- **Recipe Rating**
  - Recipe ID
  - User ID
  - Rating (1-5 stars)
  - Comments/Review
  - Rating date

- **Meal Plan**
  - Name, start date, duration
  - Planned meals (date + meal type + recipe)
  - Created by, members can view/suggest

- **Ingredient**
  - Name, category, standard units
  - Nutritional info (optional, future)

## Non-Functional Requirements

### Security
- User authentication and authorization
- Encrypt sensitive data in transit (HTTPS/TLS)
- Secure API endpoints
- Password policies and account recovery
- Family data isolation - only family members see household data

### Performance
- API response times < 500ms for typical queries
- Support offline-first mobile experience
- Efficient image storage and retrieval
- Database query optimization

### Scalability
- Support growing recipe database (hundreds to thousands of recipes)
- Handle concurrent family member updates
- Efficient recommendation algorithm even with large datasets

### Reliability
- Data backup and recovery
- Error handling and logging
- Graceful degradation for offline scenarios
- Service monitoring and alerting

## Constraints & Considerations

### Budget ($50/month limit)
- Leverage Azure free tier where possible
- Consider:
  - Shared databases (not premium)
  - Function App with consumption pricing vs. App Service
  - Image optimization and CDN only if needed
  - Minimal use of premium services initially
  
### User Base
- Small, closed user group (4-5 users per household)
- Low transaction volume initially
- Can optimize database tier for low concurrency

### Mobile Considerations
- .NET MAUI code sharing between iOS and Android
- Native API access for camera (recipe photos)
- Permissions management for both platforms

## Future Enhancements (Post-MVP)
- Nutritional information tracking
- Dietary restriction filters
- Recipe difficulty levels
- User-generated recipe categories
- Social sharing (within family)
- Email/push notifications for meal plan assignments
- Barcode scanning for ingredient tracking
- Integration with grocery delivery services
- Allergen tracking and warnings

## Success Criteria
- [ ] Family members can create, view, and rate recipes on mobile
- [ ] GenAI recipe extraction works from images, URLs, and text with >80% accuracy
- [ ] Meal plans can be created and shared with family
- [ ] Recipe recommendations appear based on ratings and ingredients
- [ ] App works offline and syncs when reconnected
- [ ] Monthly Azure costs stay under $50
- [ ] App loads recipes and meal plans in < 2 seconds on 4G connection
- [ ] All family members can securely access only their household data
