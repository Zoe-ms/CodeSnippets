using System;
using System.Collections.ObjectModel;
using Microsoft.OData.Core;
using Microsoft.OData.Core.UriBuilder;
using Microsoft.OData.Core.UriParser;
using Microsoft.OData.Core.UriParser.Semantic;
using Microsoft.OData.Core.UriParser.TreeNodeKinds;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Library;

namespace FiltingUri
{
    class Program
    {
        static void Main(string[] args)
        {
            BuildUriForSingleNav();
        }

        // http://odata.org/PeopleSet?$filter=Company/Revenue eq 5
        public static void BuildUriForSingleNav()
        {
            var model = CreateServiceEdmModel("SingleNav");

            var peopleType = model.FindDeclaredType("SingleNav.Person") as IEdmEntityType;
            var peopleSet = model.EntityContainer.FindEntitySet("PeopleSet");
            var navigationProperty = peopleType.FindProperty("Company") as IEdmNavigationProperty;
            var companyType = model.FindDeclaredType("SingleNav.Company") as IEdmEntityType;
            var property = companyType.FindProperty("Revenue") as IEdmProperty;

            EdmEntityTypeReference topEntityTypReference = new EdmEntityTypeReference(peopleType, isNullable: false);
            EntityRangeVariable topIterator = new EntityRangeVariable(
                "$it",
                topEntityTypReference,
                peopleSet);
            EntityRangeVariableReferenceNode topIteratorReference = new EntityRangeVariableReferenceNode(
                "$it",
                topIterator);
            EntityRangeVariableReferenceNode currentSourceIteratorReference = topIteratorReference;

            SingleValueNode source = null;

            if (navigationProperty.TargetMultiplicity() == EdmMultiplicity.ZeroOrOne)
            {
                source = new SingleNavigationNode(navigationProperty, currentSourceIteratorReference);
            }

            var singleNode = new SingleValuePropertyAccessNode(source, property);

            var binaryNode = new BinaryOperatorNode(BinaryOperatorKind.Equal, singleNode, new ConstantNode(5, "5"));

            var filterClause = new FilterClause(binaryNode, topIterator);

            var path = new ODataUriParser(model, new Uri("http://odata.org"), new Uri("http://odata.org/PeopleSet")).ParsePath();

            var uri = GetODataUri(new Uri("http://odata.org"), path, null, filterClause, null, false);

            ODataUriBuilder odataUriBuilder = new ODataUriBuilder(ODataUrlConventions.Default, uri);
            var requestUri =  odataUriBuilder.BuildUri();
        }

        // http://odata.org/CompanySet?$filter=Employees/any(e0:e0 eq 5)
        public static void BuildUriForMultiNav()
        {
            var model = CreateServiceEdmModel("MultiNav");

            var companyType = model.FindDeclaredType("MultiNav.Company") as IEdmEntityType;
            var companySet = model.EntityContainer.FindEntitySet("CompanySet");
            var navigationProperty = companyType.FindProperty("Employees") as IEdmNavigationProperty;

            var peopleType = model.FindDeclaredType("MultiNav.Person") as IEdmEntityType;
            var property = peopleType.FindProperty("Age") as IEdmProperty;

            EdmEntityTypeReference topEntityTypReference = new EdmEntityTypeReference(companyType, isNullable: false);
            EntityRangeVariable topIterator = new EntityRangeVariable(
                "$it",
                topEntityTypReference,
                companySet);
            EntityRangeVariableReferenceNode topIteratorReference = new EntityRangeVariableReferenceNode(
                "$it",
                topIterator);
            EntityRangeVariableReferenceNode currentSourceIteratorReference = topIteratorReference;

            CollectionNavigationNode source = null;
            EntityRangeVariable currentIterator = null;
            EntityRangeVariableReferenceNode currentIteratorReference = null;

            if (navigationProperty.TargetMultiplicity() == EdmMultiplicity.Many)
            {
                source = new CollectionNavigationNode(navigationProperty, currentSourceIteratorReference);
                currentIterator = new EntityRangeVariable("e0", GetEdmTypeReference(companySet), source);
                currentIteratorReference = new EntityRangeVariableReferenceNode(currentIterator.Name, currentIterator);
            }

            var binaryNode = new BinaryOperatorNode(BinaryOperatorKind.Equal, currentIteratorReference, new ConstantNode(5, "5"));

            var filterClause = new FilterClause(binaryNode, currentIterator);

            var anyNode = new AnyNode(new Collection<RangeVariable> { currentIterator }, currentIterator)
            {
                Body = filterClause.Expression,
                Source = source
            };

            var outfilterClause = new FilterClause(anyNode, topIterator);
            var path = new ODataUriParser(model, new Uri("http://odata.org"), new Uri("http://odata.org/CompanySet")).ParsePath();
            var uri = GetODataUri(new Uri("http://odata.org"), path, null, outfilterClause, null, false);
            ODataUriBuilder odataUriBuilder = new ODataUriBuilder(ODataUrlConventions.Default, uri);
            var requestUri = odataUriBuilder.BuildUri();
        }

        // http://odata.org/Customers?$filter=Orders/any(o1:o1/Employee/Territories/any(t1:t1/TerritoriesID eq 5))
        public static void ConstructFilter()
        {
            var model = CreateModel("Filtering");

            // Customer -> Orders (1:n)
            var customer = model.FindDeclaredType("Filtering.Customer") as IEdmEntityType;
            var customerSet = model.EntityContainer.FindEntitySet("Customers");
            var ordersInCustomer = customer.FindProperty("Orders") as IEdmNavigationProperty;

            // Orders -> Employee (1:1)
            var order = model.FindDeclaredType("Filtering.Order") as IEdmEntityType;
            var orderSet = model.EntityContainer.FindEntitySet("Orders");
            var employeeInOrder = order.FindProperty("Employee") as IEdmNavigationProperty;

            // Employee -> Territory (1:n)
            var employee = model.FindDeclaredType("Filtering.Employee") as IEdmEntityType;
            var employeeSet = model.EntityContainer.FindEntitySet("Employees");
            var territoryInEmployee = employee.FindProperty("Territories") as IEdmNavigationProperty;

            var territory = model.FindDeclaredType("Filtering.Territory") as IEdmEntityType;
            var territoryId = territory.FindProperty("TerritoriesID");

            EdmEntityTypeReference topEntityTypReference = new EdmEntityTypeReference(customer, isNullable: false);
            EntityRangeVariable topIterator = new EntityRangeVariable(
                "$it",
                topEntityTypReference,
                customerSet);
            EntityRangeVariableReferenceNode topIteratorReference = new EntityRangeVariableReferenceNode(
                "$it",
                topIterator);
            EntityRangeVariableReferenceNode currentSourceIteratorReference = topIteratorReference;

            // We have navigation property: Orders
            CollectionNavigationNode source = new CollectionNavigationNode(ordersInCustomer, currentSourceIteratorReference);
            var currentIterator = new EntityRangeVariable("o1", GetEdmTypeReference(customerSet), source);
            var currentIteratorReference = new EntityRangeVariableReferenceNode(currentIterator.Name, currentIterator);

            var outerAnyNode = new AnyNode(new Collection<RangeVariable> { currentIterator }, currentIterator)
            {
                Source = source
            };

            // we have naviagation property Employee on orders
            SingleNavigationNode employNode = new SingleNavigationNode(employeeInOrder, currentIteratorReference);

            // Territories on Employee
            CollectionNavigationNode terrietoriesNode = new CollectionNavigationNode(territoryInEmployee, employNode);
            currentIterator = new EntityRangeVariable("t1", GetEdmTypeReference(employeeSet), terrietoriesNode);
            currentIteratorReference = new EntityRangeVariableReferenceNode(currentIterator.Name, currentIterator);

            // Access property
            SingleValuePropertyAccessNode accessNode = new SingleValuePropertyAccessNode(currentIteratorReference, territoryId);

            var binaryNode = new BinaryOperatorNode(BinaryOperatorKind.Equal, accessNode, new ConstantNode(5, "5"));

            var filterClause = new FilterClause(binaryNode, currentIterator);

            var innerNode = new AnyNode(new Collection<RangeVariable> { currentIterator }, currentIterator)
            {
                Body = filterClause.Expression,
                Source = terrietoriesNode
            };

            outerAnyNode.Body = innerNode;

            var outfilterClause = new FilterClause(outerAnyNode, topIterator);
            var path = new ODataUriParser(model, new Uri("http://odata.org"), new Uri("http://odata.org/Customers")).ParsePath();
            var uri = GetODataUri(new Uri("http://odata.org"), path, null, outfilterClause, null, false);
            ODataUriBuilder odataUriBuilder = new ODataUriBuilder(ODataUrlConventions.Default, uri);
            var requestUri = odataUriBuilder.BuildUri();  // http://odata.org/Customers?$filter=Orders/any(o1:o1/Employee/Territories/any(t1:t1/TerritoriesID eq 5))
        }

        // http://odata.org/Customers?$filter=Orders/any(o1:o1/Employee/EmployeeID eq 5)
        public static void ConstructFilter2()
        {
            var model = CreateModel("Filtering");

            // Customer -> Orders (1:n)
            var customer = model.FindDeclaredType("Filtering.Customer") as IEdmEntityType;
            var customerSet = model.EntityContainer.FindEntitySet("Customers");
            var ordersInCustomer = customer.FindProperty("Orders") as IEdmNavigationProperty;

            // Orders -> Employee (1:1)
            var order = model.FindDeclaredType("Filtering.Order") as IEdmEntityType;
            var orderSet = model.EntityContainer.FindEntitySet("Orders");
            var employeeInOrder = order.FindProperty("Employee") as IEdmNavigationProperty;

            var employee = model.FindDeclaredType("Filtering.Employee") as IEdmEntityType;
            var employeeSet = model.EntityContainer.FindEntitySet("Employees");
            var employeeID = employee.FindProperty("EmployeeID");

            EdmEntityTypeReference topEntityTypReference = new EdmEntityTypeReference(customer, isNullable: false);
            EntityRangeVariable topIterator = new EntityRangeVariable(
                "$it",
                topEntityTypReference,
                customerSet);
            EntityRangeVariableReferenceNode topIteratorReference = new EntityRangeVariableReferenceNode(
                "$it",
                topIterator);
            EntityRangeVariableReferenceNode currentSourceIteratorReference = topIteratorReference;

            // We have navigation property: Orders
            CollectionNavigationNode source = new CollectionNavigationNode(ordersInCustomer, currentSourceIteratorReference);
            var currentIterator = new EntityRangeVariable("o1", GetEdmTypeReference(customerSet), source);
            var currentIteratorReference = new EntityRangeVariableReferenceNode(currentIterator.Name, currentIterator);

            var outerAnyNode = new AnyNode(new Collection<RangeVariable> { currentIterator }, currentIterator)
            {
                Source = source
            };

            // we have naviagation property Employee on orders
            SingleNavigationNode employNode = new SingleNavigationNode(employeeInOrder, currentIteratorReference);

            // Access property
            SingleValuePropertyAccessNode accessNode = new SingleValuePropertyAccessNode(employNode, employeeID);

            var binaryNode = new BinaryOperatorNode(BinaryOperatorKind.Equal, accessNode, new ConstantNode(5, "5"));

            var filterClause = new FilterClause(binaryNode, currentIterator);

            outerAnyNode.Body = filterClause.Expression;

            var outfilterClause = new FilterClause(outerAnyNode, topIterator);
            var path = new ODataUriParser(model, new Uri("http://odata.org"), new Uri("http://odata.org/Customers")).ParsePath();
            var uri = GetODataUri(new Uri("http://odata.org"), path, null, outfilterClause, null, false);
            ODataUriBuilder odataUriBuilder = new ODataUriBuilder(ODataUrlConventions.Default, uri);
            var requestUri = odataUriBuilder.BuildUri();  // http://odata.org/Customers?$filter=Orders/any(o1:o1/Employee/EmployeeID eq 5)
        }

        private static IEdmEntityTypeReference GetEdmTypeReference(IEdmNavigationSource navigationSource)
        {
            if (navigationSource.Type.TypeKind == EdmTypeKind.Collection)
            {
                return ((IEdmCollectionType)navigationSource.Type).ElementType.AsEntity();
            }
            return new EdmEntityTypeReference(navigationSource.EntityType(), isNullable: false);
        }

        public static ODataUri GetODataUri(
            Uri serviceUri,
            ODataPath odataPath,
            OrderByClause orderByClause,
            FilterClause filterClause,
            SelectExpandClause selectExpandClause,
            bool? countQuery)
        {
            ODataUri odataUri = new ODataUri();
            odataUri.ServiceRoot = serviceUri;
            odataUri.QueryCount = countQuery;

            odataUri.OrderBy = orderByClause;
            odataUri.Filter = filterClause;
            odataUri.SelectAndExpand = selectExpandClause;

            odataUri.Path = odataPath;

            return odataUri;
        }

        public static IEdmModel CreateServiceEdmModel(string ns)
        {
            EdmModel model = new EdmModel();
            var defaultContainer = new EdmEntityContainer(ns, "PerfInMemoryContainer");
            model.AddElement(defaultContainer);

            var personType = new EdmEntityType(ns, "Person");
            var personIdProperty = new EdmStructuralProperty(personType, "PersonID", EdmCoreModel.Instance.GetInt32(false));
            personType.AddProperty(personIdProperty);
            personType.AddKeys(new IEdmStructuralProperty[] { personIdProperty });
            personType.AddProperty(new EdmStructuralProperty(personType, "FirstName", EdmCoreModel.Instance.GetString(false)));
            personType.AddProperty(new EdmStructuralProperty(personType, "LastName", EdmCoreModel.Instance.GetString(false)));
            personType.AddProperty(new EdmStructuralProperty(personType, "MiddleName", EdmCoreModel.Instance.GetString(true)));
            personType.AddProperty(new EdmStructuralProperty(personType, "Age", EdmCoreModel.Instance.GetInt32(false)));
            model.AddElement(personType);

            var personSet = new EdmEntitySet(defaultContainer, "PeopleSet", personType);
            defaultContainer.AddElement(personSet);

            var addressType = new EdmComplexType(ns, "Address");
            addressType.AddProperty(new EdmStructuralProperty(addressType, "Street", EdmCoreModel.Instance.GetString(false)));
            addressType.AddProperty(new EdmStructuralProperty(addressType, "City", EdmCoreModel.Instance.GetString(false)));
            addressType.AddProperty(new EdmStructuralProperty(addressType, "PostalCode", EdmCoreModel.Instance.GetString(false)));
            model.AddElement(addressType);

            var companyType = new EdmEntityType(ns, "Company");
            var companyId = new EdmStructuralProperty(companyType, "CompanyID", EdmCoreModel.Instance.GetInt32(false));
            companyType.AddProperty(companyId);
            companyType.AddKeys(companyId);
            companyType.AddProperty(new EdmStructuralProperty(companyType, "Name", EdmCoreModel.Instance.GetString(true)));
            companyType.AddProperty(new EdmStructuralProperty(companyType, "Address", new EdmComplexTypeReference(addressType, true)));
            companyType.AddProperty(new EdmStructuralProperty(companyType, "Revenue", EdmCoreModel.Instance.GetInt32(false)));

            model.AddElement(companyType);

            var companySet = new EdmEntitySet(defaultContainer, "CompanySet", companyType);
            defaultContainer.AddElement(companySet);

            var companyEmployeeNavigation = companyType.AddUnidirectionalNavigation(new EdmNavigationPropertyInfo()
            {
                Name = "Employees",
                Target = personType,
                TargetMultiplicity = EdmMultiplicity.Many
            });
            companySet.AddNavigationTarget(companyEmployeeNavigation, personSet);

            var peopleCompanyNavigation = personType.AddUnidirectionalNavigation(new EdmNavigationPropertyInfo()
            {
                Name = "Company",
                Target = companyType,
                TargetMultiplicity = EdmMultiplicity.ZeroOrOne
            });
            personSet.AddNavigationTarget(peopleCompanyNavigation, companySet);

            // ResetDataSource
            var resetDataSourceAction = new EdmAction(ns, "ResetDataSource", null, false, null);
            model.AddElement(resetDataSourceAction);
            defaultContainer.AddActionImport(resetDataSourceAction);

            return model;
        }

        public static IEdmModel CreateModel(string ns)
        {
            EdmModel model = new EdmModel();
            var defaultContainer = new EdmEntityContainer(ns, "Container");
            model.AddElement(defaultContainer);

            var customerType = new EdmEntityType(ns, "Customer");
            var customerIdProperty = new EdmStructuralProperty(customerType, "CustomerID", EdmCoreModel.Instance.GetInt32(false));
            customerType.AddProperty(customerIdProperty);
            customerType.AddKeys(new IEdmStructuralProperty[] { customerIdProperty });
            model.AddElement(customerType);

            var customers = new EdmEntitySet(defaultContainer, "Customers", customerType);
            defaultContainer.AddElement(customers);

            var orderType = new EdmEntityType(ns, "Order");
            var orderId = new EdmStructuralProperty(orderType, "OrderID", EdmCoreModel.Instance.GetInt32(false));
            orderType.AddProperty(orderId);
            orderType.AddKeys(orderId);
            model.AddElement(orderType);

            var orders = new EdmEntitySet(defaultContainer, "Orders", orderType);
            defaultContainer.AddElement(orders);

            var employeeType = new EdmEntityType(ns, "Employee");
            var employeeId = new EdmStructuralProperty(employeeType, "EmployeeID", EdmCoreModel.Instance.GetInt32(false));
            employeeType.AddProperty(employeeId);
            employeeType.AddKeys(employeeId);
            model.AddElement(employeeType);

            var employees = new EdmEntitySet(defaultContainer, "Employees", employeeType);
            defaultContainer.AddElement(employees);

            var territoryType = new EdmEntityType(ns, "Territory");
            var territoryId = new EdmStructuralProperty(territoryType, "TerritoriesID", EdmCoreModel.Instance.GetInt32(false));
            territoryType.AddProperty(territoryId);
            territoryType.AddKeys(territoryId);
            model.AddElement(territoryType);

            var territories = new EdmEntitySet(defaultContainer, "Territories", territoryType);
            defaultContainer.AddElement(territories);

            var customerOrderNavigation = customerType.AddUnidirectionalNavigation(new EdmNavigationPropertyInfo()
            {
                Name = "Orders",
                Target = orderType,
                TargetMultiplicity = EdmMultiplicity.Many
            });
            customers.AddNavigationTarget(customerOrderNavigation, orders);

            var orderEmployeeNavigation = orderType.AddUnidirectionalNavigation(new EdmNavigationPropertyInfo()
            {
                Name = "Employee",
                Target = employeeType,
                TargetMultiplicity = EdmMultiplicity.ZeroOrOne
            });
            orders.AddNavigationTarget(orderEmployeeNavigation, employees);

            var employeeTerritoryNavigation = employeeType.AddUnidirectionalNavigation(new EdmNavigationPropertyInfo()
            {
                Name = "Territories",
                Target = territoryType,
                TargetMultiplicity = EdmMultiplicity.Many
            });
            employees.AddNavigationTarget(employeeTerritoryNavigation, territories);

            return model;
        }
    }
}
