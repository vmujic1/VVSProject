using Microsoft.EntityFrameworkCore;
using Moq;
using Ayana.Controllers;
using Ayana.Data;
using Ayana.Models;
using Ayana.Paterni;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.ComponentModel;

namespace AyanaTests
{

    [TestClass]
    public class DtoRequestsControllerTests
    {
        private Mock<IDiscountCodeVerifier> discountCodeVerifierMock;
        private Mock<ApplicationDbContext> dbContextMock;
        private DtoRequestsController controller;
        private string userId = "testUserId";
        private int productId = 1;

        [TestInitialize]
        public void TestInitialize()
        {
            discountCodeVerifierMock = new Mock<IDiscountCodeVerifier>();
            dbContextMock = new Mock<ApplicationDbContext>();
            controller = new DtoRequestsController(dbContextMock.Object, discountCodeVerifierMock.Object);
        }

        private Mock<DbSet<T>> GetDbSetMock<T>(List<T> data) where T : class
        {
            var dbSetMock = new Mock<DbSet<T>>();
            dbSetMock.As<IQueryable<T>>().Setup(m => m.Provider).Returns(data.AsQueryable().Provider);
            dbSetMock.As<IQueryable<T>>().Setup(m => m.Expression).Returns(data.AsQueryable().Expression);
            dbSetMock.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(data.AsQueryable().ElementType);
            dbSetMock.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(data.GetEnumerator());
            return dbSetMock;
        }

        // written by : Aida Zametica
        [TestMethod]
        public async Task RemoveItem_WhenProductQuantityMoreThenOne_DecreasesQuantity()
        {
            var cartList = new List<Cart>
            {
                new Cart { CustomerID = userId, ProductID = 1, ProductQuantity = 1 }, 
                new Cart { CustomerID = "otherUserId", ProductID = 2, ProductQuantity = 1 },
            };

            var cartDbSetMock = GetDbSetMock(cartList);

            dbContextMock.Setup(d => d.Cart).Returns(cartDbSetMock.Object);

            var userMock = new Mock<ClaimsPrincipal>();
            userMock.Setup(u => u.FindFirst(ClaimTypes.NameIdentifier)).Returns(new Claim(ClaimTypes.NameIdentifier, userId));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = userMock.Object }
            };

            dbContextMock.
                Setup(m => m.Remove(It.IsAny<Cart>())).Callback<Cart>((entity) => cartList.Remove(entity));

            var result = await controller.RemoveItem(1);

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Cart", redirectResult.ActionName);
            Assert.AreEqual(0, redirectResult.RouteValues["discountAmount"]);
            Assert.AreEqual(1, redirectResult.RouteValues["discountType"]);
            Assert.AreEqual("", redirectResult.RouteValues["discountCode"]);

            var removedCartItem = cartList.SingleOrDefault(c => c.CustomerID == userId && c.ProductID == productId);
            Assert.IsNull(removedCartItem, "The item should have been removed");
        }

        // written by : Aida Zametica
        [TestMethod]
        public async Task RemoveItem_WhenProductQuantityEqualOne_RemoveProduct()
        {
            var cartList = new List<Cart>
            {
                new Cart { CustomerID = userId, ProductID = 1, ProductQuantity = 2 },
                new Cart { CustomerID = "otherUserId", ProductID = 2, ProductQuantity = 1 },
            };

            var cartDbSetMock = GetDbSetMock(cartList);
         
            dbContextMock.Setup(d => d.Cart).Returns(cartDbSetMock.Object);

            var userMock = new Mock<ClaimsPrincipal>();
            userMock.Setup(u => u.FindFirst(ClaimTypes.NameIdentifier)).Returns(new Claim(ClaimTypes.NameIdentifier, userId));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = userMock.Object }
            };

            var result = await controller.RemoveItem(1);

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("Cart", redirectResult.ActionName);
            Assert.AreEqual(0, redirectResult.RouteValues["discountAmount"]);
            Assert.AreEqual(1, redirectResult.RouteValues["discountType"]);
            Assert.AreEqual("", redirectResult.RouteValues["discountCode"]);

            var removedCartItem = cartList.SingleOrDefault(c => c.CustomerID == userId && c.ProductID == productId);
            Assert.AreEqual(1, removedCartItem?.ProductQuantity, "The item quantity should have been decreased in the cart.");

        }


        // written by : Aida Zametica
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task RemoveItem_WhenUserIdIsNull_ThrowsArgumentNullException()
        {
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            mockHttpContextAccessor.Setup(a => a.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)).Returns((Claim)null);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            await controller.RemoveItem(0);
        }

        // written by : Aida Zametica
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public async Task ApplyDiscount_ValidCodeAndNotExpired_ShouldRedirectToCartWithDiscount(DiscountType discountType)
        {
            discountCodeVerifierMock.Setup(x => x.VerifyDiscountCode("ValidDiscountCode")).Returns(true);
            discountCodeVerifierMock.Setup(x => x.VerifyExperationDate("ValidDiscountCode")).Returns(true);
            discountCodeVerifierMock.Setup(x => x.GetDiscount("ValidDiscountCode")).Returns(new Discount
            {
                DiscountAmount = 10,
                DiscountType = discountType
            });

            var result = await controller.ApplyDiscount("ValidDiscountCode") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("ValidDiscountCode", result.RouteValues["discountCode"]);
            Assert.AreEqual("Cart", result.ActionName);
        }


        // written by : Aida Zametica
        [TestMethod]
        public async Task ApplyDiscount_ValidCodeAndExpired_ShouldRedirectToCartWithoutDiscount()
        {
            discountCodeVerifierMock.Setup(x => x.VerifyDiscountCode("ValidDiscountCode")).Returns(true);
            discountCodeVerifierMock.Setup(x => x.VerifyExperationDate("ValidDiscountCode")).Returns(false);
            discountCodeVerifierMock.Setup(x => x.GetDiscount("ValidDiscountCode")).Returns(new Discount
            {
                DiscountAmount = 10,
                DiscountType = DiscountType.AmountOff
            });

            var result = await controller.ApplyDiscount("ValidDiscountCode") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Code is expired...", result.RouteValues["discountCode"]);
            Assert.AreEqual("Cart", result.ActionName);
        }

        // written by : Aida Zametica
        [TestMethod]
        public async Task ApplyDiscount_InvalidCode_ShouldRedirectToCartWithoutDiscount()
        {
            discountCodeVerifierMock.Setup(x => x.VerifyDiscountCode("ValidDiscountCode")).Returns(false);
            discountCodeVerifierMock.Setup(x => x.GetDiscount("ValidDiscountCode")).Returns(new Discount
            {
                DiscountAmount = 10,
                DiscountType = DiscountType.AmountOff
            });

            var result = await controller.ApplyDiscount("ValidDiscountCode") as RedirectToActionResult;

            Assert.IsNotNull(result);
            Assert.AreEqual("Wrong code, try again...", result.RouteValues["discountCode"]);
            Assert.AreEqual("Cart", result.ActionName);
        }

        // written by : Aida Zametica
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public async Task CalculateDiscount_ValidDiscountAndNotExpired_ShouldApplyDiscountCorrectly(DiscountType discountType)
        {
            var payment = new Payment
            {
                PayedAmount = 100,
                Discount = null // zbog pokrivenosti
            };

            var discount = new Discount
            {
                DiscountCode = "ValidDiscountCode",
                DiscountBegins = new DateTime(), // zbog pokrivenosti
                DiscountEnds = new DateTime(), // zbog pokrivenosti

            };

            discountCodeVerifierMock.Setup(x => x.VerifyDiscountCode("ValidDiscountCode")).Returns(true);
            discountCodeVerifierMock.Setup(x => x.VerifyExperationDate("ValidDiscountCode")).Returns(true);
            discountCodeVerifierMock.Setup(x => x.GetDiscount("ValidDiscountCode")).Returns(new Discount
            {
                DiscountID = 1,
                DiscountAmount = 10,
                DiscountType = discountType
            });

            var result = await controller.CalculateDiscount(payment, discount);

            Assert.AreEqual(90, result.totalWithDiscount);
            Assert.AreEqual(1, result.discountId);
            Assert.AreEqual(10, result.discountAmount);
        }

        // written by : Aida Zametica
        [TestMethod]
        public async Task AddToCart_WhenProductItemExists_IncreaseQuantity()
        {
            var cartList = new List<Cart>
            {
                new Cart { CustomerID = userId, ProductID = 1, ProductQuantity = 1 },
                new Cart { CustomerID = "otherUserId", ProductID = 2, ProductQuantity = 1 },
            };

            var cartDbSetMock = GetDbSetMock(cartList);

            dbContextMock.Setup(d => d.Cart).Returns(cartDbSetMock.Object);

            var userMock = new Mock<ClaimsPrincipal>();
            userMock.Setup(u => u.FindFirst(ClaimTypes.NameIdentifier)).Returns(new Claim(ClaimTypes.NameIdentifier, userId));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = userMock.Object }
            };

            var result = await controller.AddToCart(1);

            var existingCartItem = cartList.SingleOrDefault(c => c.CustomerID == userId && c.ProductID == productId);
            Assert.IsNotNull(existingCartItem);
            Assert.AreEqual(2, existingCartItem.ProductQuantity);

            cartDbSetMock.Verify(d => d.Add(It.IsAny<Cart>()), Times.Never);
        }

        // written by : Aida Zametica
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task AddToCart_WhenUserIdIsNull_ThrowsArgumentNullException()
        {
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            mockHttpContextAccessor.Setup(a => a.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)).Returns((Claim)null);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };
           
            await controller.AddToCart(1); 
        }

        // written by: Aida Zametica
        [TestMethod]
        public async Task AddToCart_WhenProductItemNotFound_CreateCart()
        {
            var cartList = new List<Cart>
            {
                new Cart { CustomerID = "otherUserId", ProductID = 2, ProductQuantity = 1 },
            };

            var cartDbSetMock = GetDbSetMock(cartList);

            dbContextMock.Setup(d => d.Cart).Returns(cartDbSetMock.Object);

            var userMock = new Mock<ClaimsPrincipal>();
            userMock.Setup(u => u.FindFirst(ClaimTypes.NameIdentifier)).Returns(new Claim(ClaimTypes.NameIdentifier, userId));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = userMock.Object }
            };

            dbContextMock.
             Setup(m => m.Add(It.IsAny<Cart>())).Callback<Cart>((entity) => cartList.Add(entity));

            var result = await controller.AddToCart(productId);
            Assert.AreEqual(2, cartList.Count);

            var newCartItem = cartList.SingleOrDefault(c => c.CustomerID == userId && c.ProductID == productId);
            Assert.AreEqual(1, newCartItem.ProductQuantity);
        }

        // written by : Aida Zametica
        [TestMethod]
        [DataRow(123, "Address123", 0)]
        [DataRow(null,"Address456", 1)]
        public async Task SavePaymentData_ShouldSavePaymentCorrectly(int? bankAccount, string deliveryAddress, PaymentType paymentType)
        {
            var payment = new Payment
            {
                BankAccount = bankAccount,
                DeliveryAddress = deliveryAddress,
                PaymentType =paymentType,
            };

            double totalWithDiscount = 50.0;
            int? discountId = 1;

            var result = await controller.SavePaymentData(payment, totalWithDiscount, discountId);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result);
            Assert.AreEqual(result.BankAccount,bankAccount);
            Assert.AreEqual(result.DeliveryAddress, deliveryAddress);
            Assert.AreEqual(result.PaymentType, paymentType);
            Assert.AreEqual(result.PayedAmount, totalWithDiscount);
            Assert.AreEqual(result.DiscountID, discountId);
        }

        // written by : Aida Zametica
        [TestMethod]
        public async Task SaveOrderData_ShouldSaveOrderCorrectly()
        {
            double totalWithDiscount = 50.0;
            Payment paymentForOrder = new Payment { PaymentID = 1 };

            var order = new Order
            {
                DeliveryDate=new DateTime(),
                personalMessage = "Test message"
            };

            var result = await controller.SaveOrderData(order, userId, paymentForOrder,totalWithDiscount);

            Assert.IsNotNull(result);
            Assert.AreEqual(result.TotalAmountToPay, totalWithDiscount);
            Assert.AreEqual(result.CustomerID, userId);
            Assert.AreEqual(result.PaymentID,paymentForOrder.PaymentID);
            Assert.IsFalse(result.IsOrderSent);
            Assert.IsNull(result.Rating);
        }

        // written by : Aida Zametica
        [TestMethod]
        public void ThankYou_ReturnsViewWithOrderType()
        {
            var orderType = "TestOrderType";

            var result = controller.ThankYou(orderType) as ViewResult;

            Assert.IsNotNull(result);
            Assert.AreEqual(orderType, result.ViewData["OrderType"]);
        }

        // written by : Aida Zametica
        [TestMethod]
        [DataRow("0")]
        [DataRow("1")]
        public void Cart_ReturnsViewBagUpdated(string discountType)
        {
            var cartList = new List<Cart>
            {
                new Cart { CustomerID = userId, ProductID = 1, ProductQuantity = 1 },
                new Cart { CustomerID = "otherUserId", ProductID = 2, ProductQuantity = 1 },
            };

            var cartDbSetMock = GetDbSetMock(cartList);

            dbContextMock.Setup(d => d.Cart).Returns(cartDbSetMock.Object);

            var controllerMock = new Mock<DtoRequestsController>(dbContextMock.Object, discountCodeVerifierMock.Object);

            controllerMock
                .Setup(c => c.GetCartProducts(It.IsAny<List<Cart>>()))
                .Returns<List<Cart>>(carts =>
                {
                    var cartProducts = new List<List<Product>>();

                    foreach (var cart in carts)
                    {
                        var products = cart.ProductID == 1
                            ? new List<Product> { new Product { ProductID = 1, Name = "testProduct", ImageUrl = "testImageUrl", FlowerType = "testFlowerType", Stock = 0, Category = "testCategory",Description="testDescription",productType="testProductType", Price = 20 } }
                            : new List<Product>();

                        cartProducts.Add(products);
                    }

                    return cartProducts;
                });

            var userMock = new Mock<ClaimsPrincipal>();
            userMock.Setup(u => u.FindFirst(ClaimTypes.NameIdentifier)).Returns(new Claim(ClaimTypes.NameIdentifier, userId));
            
            controllerMock.Object.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = userMock.Object }
            };

            controllerMock.Object.Cart("10", discountType, "DISCOUNTCODE");

            var viewBag = controllerMock.Object.ViewBag;

            var userCarts = viewBag.UserCarts as List<Cart>;
            var cartProducts = viewBag.CartProducts as List<List<Product>>;
            var discountCode = viewBag.DiscountCode as string;
            var totalAmountToPay = viewBag.TotalAmountToPay as double?;

            controllerMock.Verify(c => c.GetCartProducts(It.IsAny<List<Cart>>()), Times.Once);
        }

        // written by : Aida Zametica
        [TestMethod]
        public void GetCartProducts_ReturnsListOfProductsForEachCart()
        {
            var product1 = new Product{ ProductID = 1, Price = 20  };
            var product2 = new Product { ProductID = 2, Price = 30 };


            var cartList = new List<Cart>
            {
                new Cart { CartID = 1, CustomerID = userId, ProductID = 1,Product = product1, ProductQuantity = 1, Customer = null },
                new Cart { CartID = 2, CustomerID = userId, ProductID = 2,Product = product2, ProductQuantity = 1, Customer = null },
            };

            var cartDbSetMock = GetDbSetMock(cartList);

            dbContextMock.Setup(d => d.Cart).Returns(cartDbSetMock.Object);

            var result = controller.GetCartProducts(cartList);

            Assert.IsNotNull(result);

            Assert.AreEqual(cartList.Count, result.Count);
        }
        
        // written by : Aida Zametica
        [TestMethod]
        public async Task OrderCreate_ValidInput_ReturnsRedirect()
        {
            var userList = new List<ApplicationUser>
            {
                new ApplicationUser { Id = userId , FullName = "testUser", EmailAddress = "testAddress", AyanaUsername = "test", Password = "testPassword"},
                new ApplicationUser { Id = "otherUserId" },
            };

                    
        var userDbSetMock = GetDbSetMock(userList);

            dbContextMock.Setup(d => d.Users).Returns(userDbSetMock.Object);

            var userMock = new Mock<ClaimsPrincipal>();
            userMock.Setup(u => u.FindFirst(ClaimTypes.NameIdentifier)).Returns(new Claim(ClaimTypes.NameIdentifier, userId));

            var controller = new Mock<DtoRequestsController>(dbContextMock.Object, discountCodeVerifierMock.Object)
            {
                CallBase = true  
            };

            controller.Setup(c => c.CalculateDiscount(It.IsAny<Payment>(), It.IsAny<Discount>()))
                      .ReturnsAsync((20.0, 1, 5.0));  

            controller.Setup(c => c.SavePaymentData(It.IsAny<Payment>(), It.IsAny<double>(), It.IsAny<int?>()))
                      .ReturnsAsync(  new Payment
                      {
                          BankAccount = 123,
                          DeliveryAddress = "Test address",
                          PaymentType = (PaymentType)1,
                      }
            );

            controller.Setup(c => c.SaveOrderData(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<Payment>(), It.IsAny<double>()))
                      .ReturnsAsync(new Order
                      {
                          DeliveryDate = DateTime.Now,
                          personalMessage = "Test message",
                      });

            controller.Setup(c => c.ProcessCartItems(It.IsAny<string>(), It.IsAny<Order>()))
                      .Returns(Task.CompletedTask);

            controller.Object.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = userMock.Object }
            };

            var result = await controller.Object.OrderCreate(
                new Order
                {
                    DeliveryDate = DateTime.Now,
                    personalMessage = "Test message"
                },
                new Payment
                {
                    BankAccount = 123,
                    DeliveryAddress = "Test address",
                    PaymentType = (PaymentType)1,
                },
                new Discount
                {
                    DiscountCode = "TESTCODE"
                });

            Assert.IsInstanceOfType(result, typeof(RedirectResult));
            Assert.AreEqual("/DtoRequests/ThankYou?orderType=order", ((RedirectResult)result).Url);
        }

        // written by : Aida Zametica
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task OrderCreate_WhenUserIdIsNull_ThrowsArgumentNullException()
        {
            var userList = new List<ApplicationUser>
            {
                new ApplicationUser { Id = userId },
                new ApplicationUser { Id = "otherUserId" },
            };

            var userDbSetMock = GetDbSetMock(userList);

            dbContextMock.Setup(d => d.Users).Returns(userDbSetMock.Object);

            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            mockHttpContextAccessor.Setup(a => a.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)).Returns((Claim)null);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            var result = await controller.OrderCreate(
               new Order
               {
                   DeliveryDate = DateTime.Now,
                   personalMessage = "Test message"
               },
               new Payment
               {
                   BankAccount = 123,
                   DeliveryAddress = "Test address",
                   PaymentType = (PaymentType)1,
               },
               new Discount
               {
                   DiscountCode = "TESTCODE"
               });
        }

        // written by : Aida Zametica
        [TestMethod]
        public async Task ProcessCartItems_ShouldProcessItemsAndRemoveCarts()
        {
            var product1 = new Product { ProductID = 1, Price = 20 };
            var product2 = new Product { ProductID = 2, Price = 30 };

            var cartList = new List<Cart>
            {
                new Cart { CartID = 1, CustomerID = userId, ProductID = 1, Product = product1, ProductQuantity = 2 },
                new Cart { CartID = 2, CustomerID = userId, ProductID = 2, Product = product2, ProductQuantity = 1 },
            };

            var order = new Order { OrderID = 123, Customer = null, Payment = null };

            var cartDbSetMock = GetDbSetMock(cartList);

            dbContextMock.Setup(d => d.Cart).Returns(cartDbSetMock.Object);

            await controller.ProcessCartItems(userId, order);

            foreach (var cart in cartList)
            {
                dbContextMock.Verify(c => c.Cart.Remove(cart), Times.Once);
            }

            dbContextMock.Verify(c => c.SaveChanges(), Times.Once);
        }

    }
}